using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Stub
{
    /// <summary>
    /// Имитация магазина: фиксированный список товаров, покупка всегда успешна.
    /// Pending-покупки живут только в памяти и сбрасываются при перезапуске.
    /// </summary>
    public sealed class StubPurchaseService : IPurchaseService
    {
        private static readonly List<ProductInfo> Products = new List<ProductInfo>
        {
            new ProductInfo { Id = "no_ads", Title = "Убрать рекламу", Description = "Отключает межстраничную рекламу", PriceFormatted = "99 RUB" },
            new ProductInfo { Id = "coins_100", Title = "100 монет", Description = "Небольшой мешочек монет", PriceFormatted = "49 RUB" },
        };

        private readonly List<PurchaseInfo> _pending = new List<PurchaseInfo>();

        public bool IsAvailable => true;

        public UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult<IReadOnlyList<ProductInfo>>(Products);
        }

        public UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            var purchase = new PurchaseInfo { ProductId = productId, PurchaseToken = Guid.NewGuid().ToString("N") };
            _pending.Add(purchase);
            Debug.Log($"[Stub] Purchased '{productId}', token {purchase.PurchaseToken}");
            return UniTask.FromResult(PurchaseResult.Ok(purchase));
        }

        public UniTask<IReadOnlyList<PurchaseInfo>> GetPendingPurchasesAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult<IReadOnlyList<PurchaseInfo>>(_pending.ToArray());
        }

        public UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default)
        {
            _pending.RemoveAll(p => p.PurchaseToken == purchaseToken);
            Debug.Log($"[Stub] Consumed token {purchaseToken}");
            return UniTask.CompletedTask;
        }
    }
}
