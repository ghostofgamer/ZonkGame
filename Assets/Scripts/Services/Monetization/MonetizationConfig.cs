using System.Collections.Generic;

namespace Base.Services.Monetization
{
    public enum ProductKind
    {
        /// <summary>Покупается один раз и остаётся навсегда, например "без рекламы". На площадке не подтверждается (consume).</summary>
        Permanent,
        /// <summary>Расходуется, например монеты. Выдаётся игре через IPurchaseFlow.ClaimConsumable и подтверждается на площадке.</summary>
        Consumable,
    }

    /// <summary>Товар игры. Id совпадает с ID товара в консоли площадки.</summary>
    public sealed class ProductDefinition
    {
        public ProductDefinition(string id, ProductKind kind, string entitlement = null)
        {
            Id = id;
            Kind = kind;
            Entitlement = entitlement ?? id;
        }

        public string Id { get; }
        public ProductKind Kind { get; }

        /// <summary>Право, которое даёт постоянный товар. По умолчанию совпадает с Id.</summary>
        public string Entitlement { get; }
    }

    /// <summary>Когда показывать межстраничную рекламу. Время считается в секундах реального времени.</summary>
    public sealed class InterstitialRules
    {
        /// <summary>Сколько секунд после запуска рекламы нет совсем. VK запрещает рекламу сразу после запуска.</summary>
        public float FirstShowDelay = 60f;

        /// <summary>Минимум секунд между любыми показами рекламы, включая rewarded. Яндекс сам не даёт показывать чаще раза в минуту.</summary>
        public float MinInterval = 90f;

        /// <summary>Показ только на каждом N-м поводе: 1 = на каждом, 2 = через раз.</summary>
        public int EveryNthTrigger = 1;
    }

    /// <summary>Как выдавать награды, если посмотреть рекламу нельзя.</summary>
    public sealed class RewardRules
    {
        /// <summary>Реклама не загрузилась или недоступна: выдать награду бесплатно или отказать.</summary>
        public bool FreeWhenAdsUnavailable = false;

        /// <summary>Игрок купил "без рекламы": выдавать награды без просмотра.</summary>
        public bool FreeWithNoAds = false;
    }

    /// <summary>
    /// Настройки монетизации. Механизм общий для всех игр, а этот файл каждая игра правит под себя:
    /// список товаров, частота рекламы, правила наград.
    /// </summary>
    public sealed class MonetizationConfig
    {
        public IReadOnlyList<ProductDefinition> Products = new ProductDefinition[0];
        public InterstitialRules Interstitial = new InterstitialRules();
        public RewardRules Rewards = new RewardRules();

        public static MonetizationConfig CreateDefault()
        {
            return new MonetizationConfig
            {
                Products = new[]
                {
                    new ProductDefinition(EntitlementIds.NoAds, ProductKind.Permanent),
                },
            };
        }

        public ProductDefinition FindProduct(string productId)
        {
            foreach (var product in Products)
            {
                if (product.Id == productId)
                    return product;
            }

            return null;
        }
    }
}
