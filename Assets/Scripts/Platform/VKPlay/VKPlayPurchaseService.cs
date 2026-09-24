using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKPlay
{
    /// <summary>
    /// Покупки VK Play. TODO: VKWebAppShowOrderBox (type "item"), список товаров и подтверждение
    /// идут через серверный callback VK, поэтому здесь может понадобиться свой бэкенд.
    /// </summary>
    public sealed class VKPlayPurchaseService : IPurchaseService
    {
        private static readonly IReadOnlyList<ProductInfo> Empty = new ProductInfo[0];
        private static readonly IReadOnlyList<PurchaseInfo> NoPurchases = new PurchaseInfo[0];

        public bool IsAvailable => false;

        public UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] GetProducts: SDK not integrated yet");
            return UniTask.FromResult(Empty);
        }

        public UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            Debug.LogWarning($"[VKPlay] Purchase '{productId}': SDK not integrated yet");
            return UniTask.FromResult(PurchaseResult.Fail("not_integrated"));
        }

        public UniTask<IReadOnlyList<PurchaseInfo>> GetPendingPurchasesAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(NoPurchases);
        }

        public UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] Consume: SDK not integrated yet");
            return UniTask.CompletedTask;
        }
    }
}
