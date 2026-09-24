using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.RuStore
{
    /// <summary>
    /// Покупки RuStore. TODO: RuStore Billing SDK для Unity: RuStoreBillingClient.Init,
    /// GetProducts, PurchaseProduct, GetPurchases, ConfirmPurchase.
    /// </summary>
    public sealed class RuStorePurchaseService : IPurchaseService
    {
        private static readonly IReadOnlyList<ProductInfo> Empty = new ProductInfo[0];
        private static readonly IReadOnlyList<PurchaseInfo> NoPurchases = new PurchaseInfo[0];

        public bool IsAvailable => false;

        public UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[RuStore] GetProducts: Billing SDK not integrated yet");
            return UniTask.FromResult(Empty);
        }

        public UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            Debug.LogWarning($"[RuStore] Purchase '{productId}': Billing SDK not integrated yet");
            return UniTask.FromResult(PurchaseResult.Fail("not_integrated"));
        }

        public UniTask<IReadOnlyList<PurchaseInfo>> GetPendingPurchasesAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(NoPurchases);
        }

        public UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[RuStore] Consume: Billing SDK not integrated yet");
            return UniTask.CompletedTask;
        }
    }
}
