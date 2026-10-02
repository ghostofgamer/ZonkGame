using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Раскладка магазина: у каждого предмета ОДИН способ получения — монеты, реклама или покупка за деньги.
    /// В каждой вкладке поровну: 2 предмета за монеты, 2 за рекламу, 2 за покупку (у особых костей 4 / 3 / 3).
    /// Путь «победите соперника» (подсказка ProgressPriceOption) у кого он есть — остаётся дополнительным.
    ///
    /// Применяется один раз ко всем предметам (GameConfig.ShopLayoutVersion), чтобы дальше не затирать ручные правки,
    /// и всегда — к предметам из таблицы, у которых цены нет совсем (иначе новый предмет достался бы всем даром).
    /// Следующая перестановка — новая таблица и версия, эту не удалять.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const int ShopLayoutVersion = 4;

        private enum Buy
        {
            Coins,
            Ads,
            Purchase,
        }

        /// <summary>v3: ID предмета → способ и число (монет или реклам; для покупки — не важно, ID товара = ID предмета).</summary>
        private static readonly (string id, Buy how, int value)[] ShopLayoutV3 =
        {
            // Локации (базовая — дом).
            ("env_tavern", Buy.Coins, 5000), ("env_basement", Buy.Coins, 6000),
            ("env_attic", Buy.Ads, 5), ("env_garden", Buy.Ads, 6),
            ("env_beach", Buy.Purchase, 0), ("env_ship", Buy.Purchase, 0),

            // Столы (базовый — дубовый).
            ("table_dark", Buy.Coins, 1500), ("table_marble", Buy.Coins, 3000),
            ("table_walnut", Buy.Ads, 4), ("table_birch", Buy.Ads, 4),
            ("table_royal", Buy.Purchase, 0), ("table_gold", Buy.Purchase, 0),

            // Сукно (базовое — зелёное).
            ("felt_red", Buy.Coins, 1000), ("felt_purple", Buy.Coins, 2000),
            ("felt_blue", Buy.Ads, 3), ("felt_teal", Buy.Ads, 4),
            ("felt_royal", Buy.Purchase, 0), ("felt_gold", Buy.Purchase, 0),

            // Стаканы (базовый — кожаный).
            ("cup_wood", Buy.Coins, 1200), ("cup_bronze", Buy.Coins, 2500),
            ("cup_silver", Buy.Ads, 5), ("cup_jade", Buy.Ads, 5),
            ("cup_gold", Buy.Purchase, 0), ("cup_ruby", Buy.Purchase, 0),

            // Вид костей (базовый — слоновая кость).
            ("skin_ruby", Buy.Coins, 1200), ("skin_obsidian", Buy.Coins, 2500),
            ("skin_jade", Buy.Ads, 3), ("skin_pearl", Buy.Ads, 4),
            ("skin_gold", Buy.Purchase, 0), ("skin_emerald", Buy.Purchase, 0),

            // Лампы (базовая — абажур).
            ("lamp_lantern", Buy.Coins, 1500), ("lamp_brass", Buy.Coins, 2000),
            ("lamp_paper", Buy.Ads, 4), ("lamp_glass", Buy.Ads, 4),
            ("lamp_crystal", Buy.Purchase, 0), ("lamp_cage", Buy.Purchase, 0),

            // Стили броска (базовый — классический; бабушкин и пиратский — только награды боссов).
            ("roll_calm", Buy.Coins, 1200), ("roll_quick", Buy.Coins, 1200),
            ("roll_wild", Buy.Ads, 4), ("roll_showman", Buy.Ads, 3),
            ("roll_royal", Buy.Purchase, 0), ("roll_storm", Buy.Purchase, 0),

            // Особые кости: 4 за монеты, 3 за рекламу, 3 за покупку; каждая ещё и награда соперника.
            ("die_worn", Buy.Coins, 1000), ("die_middle", Buy.Coins, 1000), ("die_bone", Buy.Coins, 1500), ("die_fives", Buy.Coins, 1500),
            ("die_even", Buy.Ads, 5), ("die_odd", Buy.Ads, 4), ("die_edges", Buy.Ads, 4),
            ("die_sixes", Buy.Purchase, 0), ("die_lucky", Buy.Purchase, 0), ("die_sharper", Buy.Purchase, 0),

            // Наборы — только за покупку.
            ("theme_pirate", Buy.Purchase, 0),
        };

        /// <summary>
        /// v4 (01.10.2026): вкладка «Стакан» — свои модели вместо перекрасок, 3 / 3 / 3. Базовый — кожаный.
        /// </summary>
        private static readonly (string id, Buy how, int value)[] ShopLayoutV4 =
        {
            ("cup_wood", Buy.Coins, 1200), ("cup_barrel", Buy.Coins, 2500), ("cup_clay", Buy.Coins, 3500),
            ("cup_coconut", Buy.Ads, 5), ("cup_stone", Buy.Ads, 6), ("cup_bone", Buy.Ads, 6),
            ("cup_gold", Buy.Purchase, 0), ("cup_goblet", Buy.Purchase, 0), ("cup_copper", Buy.Purchase, 0),
        };

        /// <summary>Только награды боссов: цена за монеты или деньги снимается, подсказка остаётся.</summary>
        private static readonly string[] BossOnlyV3 = { "roll_granny", "roll_pirate" };

        private static void ApplyShopLayout(GameConfig config, CurrencyConfig coins)
        {
            var content = ContentById<ContentConfig>();

            // Таблицы по порядку версий: каждая применяется ко всем своим предметам один раз, к бесплатным — всегда.
            var firstTime = config.ShopLayoutVersion < 3;
            ApplyLayoutTable(ShopLayoutV3, firstTime, content, coins);
            var firstTimeV4 = config.ShopLayoutVersion < 4;
            ApplyLayoutTable(ShopLayoutV4, firstTimeV4, content, coins);

            if (firstTime)
            {
                foreach (var id in BossOnlyV3)
                {
                    if (content.TryGetValue(id, out var item) && Pricing.PriceOf(item) is Price price)
                        SetSinglePrice(item, price, null);
                }

                Debug.Log("[Setup] Shop layout v3 applied: one way to get each item, 2 / 2 / 2 per tab");
            }

            if (firstTimeV4)
                Debug.Log("[Setup] Shop layout v4 applied: cup tab — own models, 3 / 3 / 3");

            if (config.ShopLayoutVersion < ShopLayoutVersion)
            {
                config.ShopLayoutVersion = ShopLayoutVersion;
                EditorUtility.SetDirty(config);
            }
        }

        private static void ApplyLayoutTable((string id, Buy how, int value)[] table, bool firstTime,
            Dictionary<string, ContentConfig> content, CurrencyConfig coins)
        {
            foreach (var (id, how, value) in table)
            {
                if (!content.TryGetValue(id, out var item))
                    continue;

                var price = Pricing.PriceOf(item);
                if (price == null || (!firstTime && !price.IsFree))
                    continue;

                SetSinglePrice(item, price, how switch
                {
                    Buy.Coins => Coins(coins, value),
                    Buy.Ads => new RewardedAdPriceOption { AdsRequired = value },
                    _ => (PriceOption)new PurchasePriceOption { ProductId = id },
                });
            }
        }

        /// <summary>Оставить у цены подсказку «за прохождение» (если была) и один вариант option (null — без него).</summary>
        private static void SetSinglePrice(ContentConfig item, Price price, PriceOption option)
        {
            price.Options.RemoveAll(o => !(o is ProgressPriceOption progress) || string.IsNullOrEmpty(progress.HintKey));
            if (option != null)
                price.Options.Add(option);
            EditorUtility.SetDirty(item);
        }
    }
}
