using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Base.Platform;

namespace Base.Services.Monetization
{
    public enum PurchaseOutcome
    {
        /// <summary>Товар оплачен и выдан.</summary>
        Success,
        /// <summary>Игрок отказался или площадка вернула ошибку.</summary>
        Failed,
        /// <summary>Покупки на этой площадке недоступны: кнопку магазина показывать не нужно.</summary>
        Unavailable,
        /// <summary>Товара нет в MonetizationConfig.</summary>
        UnknownProduct,
    }

    /// <summary>
    /// Покупки на уровне игры: покупка, выдача, восстановление при запуске.
    /// Постоянные товары превращаются в права (IEntitlements), расходуемые копятся в сохранении,
    /// пока игра их не заберёт через ClaimConsumable. Так покупка не теряется, даже если игра
    /// закрылась между оплатой и выдачей.
    /// </summary>
    public interface IPurchaseFlow
    {
        /// <summary>Покупки есть на площадке. Если нет, магазин в игре не показывать.</summary>
        bool IsAvailable { get; }

        /// <summary>Товар выдан: постоянный стал правом или расходуемый ждёт ClaimConsumable. Аргумент: ID товара.</summary>
        event Action<string> Delivered;

        /// <summary>Каталог площадки: названия и цены для витрины.</summary>
        UniTask<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken cancellationToken = default);

        UniTask<PurchaseOutcome> BuyAsync(string productId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Выдаёт покупки, которые оплачены, но не выданы, и восстанавливает постоянные товары.
        /// Вызывается при запуске из PlatformInitializer.
        /// </summary>
        UniTask RestoreAsync(CancellationToken cancellationToken = default);

        /// <summary>Сколько единиц расходуемого товара оплачено и ещё не выдано в игре. Забирает их и сохраняет.</summary>
        int ClaimConsumable(string productId);
    }
}
