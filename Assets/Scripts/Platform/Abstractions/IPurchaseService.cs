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

    /// <summary>Список покупок с площадки для сверки прав. Ok = false: площадка не ответила, права не трогать.</summary>
    public sealed class PurchaseList
    {
        public static readonly PurchaseList Failed = new PurchaseList { Ok = false, Items = new PurchaseInfo[0] };

        public bool Ok;
        public IReadOnlyList<PurchaseInfo> Items;
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

        /// <summary>
        /// Площадка отдаёт постоянные покупки в списке всегда (их не подтверждают), поэтому по списку можно сверять права:
        /// право есть, а покупки в ответе нет — право снимается (защита от правки сохранения). false — сверки нет.
        /// </summary>
        bool ListsPermanentPurchases { get; }

        /// <summary>Все неподтверждённые покупки (вместе с постоянными) и ответила ли площадка.</summary>
        UniTask<PurchaseList> GetPurchasesAsync(CancellationToken cancellationToken = default);

        /// <summary>Подтвердить покупку после выдачи товара.</summary>
        UniTask ConsumeAsync(string purchaseToken, CancellationToken cancellationToken = default);
    }
}
