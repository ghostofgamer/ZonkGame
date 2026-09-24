using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Platform
{
    public sealed class ProductInfo
    {
        public string Id;
        public string Title;
        public string Description;
        /// <summary>Цена в формате площадки, например "99 RUB" или "10 YAN".</summary>
        public string PriceFormatted;
        public string ImageUrl;
    }

    public sealed class PurchaseInfo
    {
        public string ProductId;
        /// <summary>Токен для подтверждения (consume) покупки на площадке.</summary>
        public string PurchaseToken;
    }

    public sealed class PurchaseResult
    {
        public bool Success;
        public PurchaseInfo Purchase;
        public string Error;

        public static PurchaseResult Ok(PurchaseInfo purchase) => new PurchaseResult { Success = true, Purchase = purchase };
        public static PurchaseResult Fail(string error) => new PurchaseResult { Success = false, Error = error };
    }

    /// <summary>Внутриигровые покупки.</summary>
    public interface IPurchaseService
    {
        /// <summary>Покупки доступны на площадке и SDK платежей инициализирован.</summary>
        bool IsAvailable { get; }

        UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default);

        UniTask<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken = default);

        /// <summary>Купленные, но ещё не подтверждённые покупки. Проверять при старте.</summary>
        UniTask<IReadOnlyList<PurchaseInfo>> GetPendingPurchasesAsync(CancellationToken cancellationToken = default);

        /// <summary>Подтвердить покупку после выдачи товара.</summary>
        UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default);
    }
}
