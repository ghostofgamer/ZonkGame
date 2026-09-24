using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Yandex
{
    /// <summary>
    /// Покупки Яндекс Игр. Объект payments подгружается лениво при первом обращении:
    /// у игр без внутриигровых товаров getPayments завершается ошибкой, и это нормально.
    /// </summary>
    public sealed class YandexPurchaseService : IPurchaseService
    {
        private static readonly IReadOnlyList<ProductInfo> NoProducts = new ProductInfo[0];
        private static readonly IReadOnlyList<PurchaseInfo> NoPurchases = new PurchaseInfo[0];

        private bool _paymentsReady;
        private bool _paymentsFailed;

        public bool IsAvailable => YandexBridge.IsSupported && YandexSession.IsInitialized && !_paymentsFailed;

        public async UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            if (!await EnsurePaymentsAsync(cancellationToken))
                return NoProducts;

            try
            {
                var catalog = await YandexBridge.GetCatalogAsync(cancellationToken);
                if (catalog.items == null)
                    return NoProducts;

                var products = new List<ProductInfo>(catalog.items.Length);
                foreach (var item in catalog.items)
                {
                    products.Add(new ProductInfo
                    {
                        Id = item.id,
                        Title = item.title,
                        Description = item.description,
                        PriceFormatted = item.price,
                        ImageUrl = item.imageURI,
                    });
                }

                return products;
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] GetCatalog failed: {e.Message}");
                return NoProducts;
            }
        }

        public async UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            if (!await EnsurePaymentsAsync(cancellationToken))
                return PurchaseResult.Fail("payments_unavailable");

            try
            {
                var purchase = await YandexBridge.PurchaseAsync(productId, cancellationToken);
                return PurchaseResult.Ok(new PurchaseInfo
                {
                    ProductId = purchase.productID,
                    PurchaseToken = purchase.purchaseToken,
                });
            }
            catch (YandexBridgeException e)
            {
                // Сюда же попадает отказ игрока в окне оплаты.
                Debug.Log($"[Yandex] Purchase '{productId}' failed: {e.Message}");
                return PurchaseResult.Fail(e.Message);
            }
        }

        public async UniTask<IReadOnlyList<PurchaseInfo>> GetPendingPurchasesAsync(CancellationToken cancellationToken = default)
        {
            if (!await EnsurePaymentsAsync(cancellationToken))
                return NoPurchases;

            try
            {
                var purchases = await YandexBridge.GetPurchasesAsync(cancellationToken);
                if (purchases.items == null)
                    return NoPurchases;

                var result = new List<PurchaseInfo>(purchases.items.Length);
                foreach (var item in purchases.items)
                {
                    result.Add(new PurchaseInfo
                    {
                        ProductId = item.productID,
                        PurchaseToken = item.purchaseToken,
                    });
                }

                return result;
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] GetPurchases failed: {e.Message}");
                return NoPurchases;
            }
        }

        public async UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default)
        {
            if (!await EnsurePaymentsAsync(cancellationToken))
                return;

            try
            {
                await YandexBridge.ConsumeAsync(purchaseToken, cancellationToken);
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] Consume failed: {e.Message}");
            }
        }

        /// <summary>Готовит объект payments один раз. Повторные неудачи не пытаемся чинить.</summary>
        private async UniTask<bool> EnsurePaymentsAsync(CancellationToken cancellationToken)
        {
            if (_paymentsReady)
                return true;

            if (_paymentsFailed || !YandexBridge.IsSupported || !YandexSession.IsInitialized)
                return false;

            try
            {
                await YandexBridge.InitPaymentsAsync(cancellationToken);
                _paymentsReady = true;
                return true;
            }
            catch (Exception e)
            {
                _paymentsFailed = true;
                Debug.LogWarning($"[Yandex] Payments are unavailable: {e.Message}");
                return false;
            }
        }
    }
}
