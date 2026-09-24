using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Base.Services.Saves;
using Base.Platform;
using UnityEngine;

namespace Base.Services.Monetization
{
    /// <summary>
    /// Покупка идёт в таком порядке: оплата, запись выдачи в сохранение, запись в облако,
    /// и только потом consume на площадке. Если игра упадёт посередине, покупка останется
    /// неподтверждённой, и RestoreAsync выдаст её при следующем запуске.
    ///
    /// Чтобы расходуемый товар не выдался дважды (выдали, сохранили, а consume не прошёл),
    /// обработанные токены покупок запоминаются в сохранении.
    ///
    /// Постоянные товары не подтверждаются: площадка (Яндекс) продолжает отдавать их
    /// в списке покупок, и право восстанавливается даже на новом устройстве.
    /// </summary>
    public sealed class PurchaseFlow : IPurchaseFlow
    {
        private const string SaveKey = "purchases";
        private const int MaxRememberedTokens = 100;

        private readonly IPurchaseService _purchases;
        private readonly ISaveStore _saves;
        private readonly IEntitlements _entitlements;
        private readonly MonetizationConfig _config;

        public PurchaseFlow(IPurchaseService purchases, ISaveStore saves, IEntitlements entitlements, MonetizationConfig config)
        {
            _purchases = purchases;
            _saves = saves;
            _entitlements = entitlements;
            _config = config;
        }

        public bool IsAvailable => _purchases.IsAvailable;

        public event Action<string> Delivered;

        private PurchasesData Data
        {
            get
            {
                var data = _saves.Get<PurchasesData>(SaveKey);
                if (data.processedTokens == null) data.processedTokens = new List<string>();
                if (data.unclaimed == null) data.unclaimed = new List<string>();
                return data;
            }
        }

        public UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            return _purchases.GetProductsAsync(cancellationToken);
        }

        public async UniTask<PurchaseOutcome> BuyAsync(string productId, CancellationToken cancellationToken = default)
        {
            var product = _config.FindProduct(productId);
            if (product == null)
            {
                Debug.LogError($"[Purchases] Product '{productId}' is not in MonetizationConfig");
                return PurchaseOutcome.UnknownProduct;
            }

            if (!_purchases.IsAvailable)
                return PurchaseOutcome.Unavailable;

            await _saves.WaitLoadedAsync(cancellationToken);

            var result = await _purchases.PurchaseAsync(productId, cancellationToken);
            if (!result.Success)
            {
                Debug.Log($"[Purchases] '{productId}' not bought: {result.Error}");
                return PurchaseOutcome.Failed;
            }

            await DeliverAsync(product, result.Purchase, cancellationToken);
            return PurchaseOutcome.Success;
        }

        public async UniTask RestoreAsync(CancellationToken cancellationToken = default)
        {
            if (!_purchases.IsAvailable)
                return;

            await _saves.WaitLoadedAsync(cancellationToken);

            var pending = await _purchases.GetPendingPurchasesAsync(cancellationToken);
            foreach (var purchase in pending)
            {
                var product = _config.FindProduct(purchase.ProductId);
                if (product == null)
                {
                    // Неизвестный товар не подтверждаем: возможно, его знает более новая версия игры.
                    Debug.LogWarning($"[Purchases] Pending purchase of unknown product '{purchase.ProductId}' skipped");
                    continue;
                }

                await DeliverAsync(product, purchase, cancellationToken);
            }

            Debug.Log($"[Purchases] Restored: pending={pending.Count} entitlements=[{string.Join(", ", _entitlements.All)}]");
        }

        public int ClaimConsumable(string productId)
        {
            var unclaimed = Data.unclaimed;
            var count = unclaimed.RemoveAll(id => id == productId);
            if (count > 0)
                _saves.RequestSave();

            return count;
        }

        private async UniTask DeliverAsync(ProductDefinition product, PurchaseInfo purchase, CancellationToken cancellationToken)
        {
            if (product.Kind == ProductKind.Permanent)
            {
                var isNew = !_entitlements.Has(product.Entitlement);
                _entitlements.Grant(product.Entitlement);
                await _saves.SaveNowAsync(cancellationToken);

                if (isNew)
                    Delivered?.Invoke(product.Id);
                return;
            }

            var data = Data;
            var token = purchase.PurchaseToken;
            var alreadyDelivered = !string.IsNullOrEmpty(token) && data.processedTokens.Contains(token);

            if (!alreadyDelivered)
            {
                data.unclaimed.Add(product.Id);
                if (!string.IsNullOrEmpty(token))
                {
                    data.processedTokens.Add(token);
                    if (data.processedTokens.Count > MaxRememberedTokens)
                        data.processedTokens.RemoveRange(0, data.processedTokens.Count - MaxRememberedTokens);
                }

                await _saves.SaveNowAsync(cancellationToken);
                Debug.Log($"[Purchases] '{product.Id}' delivered");
                Delivered?.Invoke(product.Id);
            }

            // Без записи в облако выдача не переживёт перезапуск. Покупку оставляем неподтверждённой,
            // тогда в следующей сессии она выдастся снова.
            if (_saves.IsCloudWriteBlocked)
            {
                Debug.LogWarning($"[Purchases] '{product.Id}' not consumed: cloud write is blocked");
                return;
            }

            await _purchases.ConsumeAsync(token, cancellationToken);
        }

        [Serializable]
        internal sealed class PurchasesData
        {
            /// <summary>Токены расходуемых покупок, которые уже выданы.</summary>
            public List<string> processedTokens = new List<string>();

            /// <summary>Оплаченные расходуемые товары, которые игра ещё не забрала. Один элемент на единицу.</summary>
            public List<string> unclaimed = new List<string>();
        }
    }
}
