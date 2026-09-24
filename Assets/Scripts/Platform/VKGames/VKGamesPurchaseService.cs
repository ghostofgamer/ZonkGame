using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Покупки VK (VKWebAppShowOrderBox) без своего сервера невозможны: VK запрашивает описание товара
    /// и сообщает об оплате POST-запросами get_item и order_status_change на адрес обратного вызова игры.
    /// Реальные платежи работают только после публикации в каталоге, на iOS не работают.
    /// Покупки отложены до этапа монетизации, поэтому сервис всегда недоступен.
    /// </summary>
    public sealed class VKGamesPurchaseService : IPurchaseService
    {
        private const string Unavailable = "VK purchases require a game server for payment callbacks";

        private static readonly IReadOnlyList<ProductInfo> NoProducts = new ProductInfo[0];
        private static readonly IReadOnlyList<PurchaseInfo> NoPurchases = new PurchaseInfo[0];

        public bool IsAvailable => false;

        public UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(NoProducts);
        }

        public UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default)
        {
            Debug.Log($"[VKGames] Purchase '{productId}' skipped: {Unavailable}");
            return UniTask.FromResult(PurchaseResult.Fail(Unavailable));
        }

        public UniTask<IReadOnlyList<PurchaseInfo>> GetPendingPurchasesAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(NoPurchases);
        }

        public UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default)
        {
            return UniTask.CompletedTask;
        }
    }
}
