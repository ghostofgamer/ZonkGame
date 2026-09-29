using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Магазин за деньги: в каждой вкладке есть предметы за монеты, за рекламу и за покупку (правило магазина, его
    /// проверяет валидатор), пакеты монет, стартовый набор, «Без рекламы», выплаты ставок у соперников.
    /// Новые предметы — заглушки цветом материала; настоящие модели и текстуры подставляются в ассеты предметов.
    /// Уже созданным предметам недостающий вариант цены добавляется, если такого вида цены у предмета ещё нет.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static void BuildShopCatalog(CurrencyConfig coins, CurrencyConfig energy, DieConfig lucky)
        {
            var envSlot = SlotAt("environment");
            var tableSlot = SlotAt("table");
            var traySlot = SlotAt("tray");
            var cupSlot = SlotAt("cup");
            var lampSlot = SlotAt("lamp");
            var rollSlot = SlotAt("roll_style");

            // Локации: за монеты, рекламу (пляж) и покупку — все три, путь через боссов тоже остаётся.
            EnsurePurchase(ItemAt("env_tavern"), "env_tavern");
            EnsurePurchase(ItemAt("env_beach"), "env_beach");
            EnsurePurchase(ItemAt("env_ship"), "env_ship");

            // Столы: реклама — ореховый, покупка — мраморный и «Королевский» (только за деньги).
            Item("table_walnut", tableSlot, 3, new MaterialPayload { Material = ShopMat("Table_Walnut", new Color(0.36f, 0.22f, 0.12f), 0.45f) },
                Coins(coins, 1500), new RewardedAdPriceOption { AdsRequired = 4 });
            Item("table_royal", tableSlot, 4, new MaterialPayload { Material = ShopMat("Table_Royal", new Color(0.05f, 0.35f, 0.25f), 0.85f, 0.2f) },
                new PurchasePriceOption { ProductId = "table_royal" });
            EnsurePurchase(ItemAt("table_marble"), "table_marble");

            // Сукно: реклама — синее, монеты — красное и фиолетовое, покупка — «Королевское».
            Item("felt_purple", traySlot, 3, new MaterialPayload { Material = ShopMat("Felt_Purple", new Color(0.35f, 0.12f, 0.4f), 0.1f) },
                Coins(coins, 1500));
            Item("felt_royal", traySlot, 4, new MaterialPayload { Material = ShopMat("Felt_Royal", new Color(0.06f, 0.05f, 0.07f), 0.2f) },
                new PurchasePriceOption { ProductId = "felt_royal" });

            // Стаканы: реклама — серебряный, покупка — золотой и рубиновый.
            Item("cup_silver", cupSlot, 3, new MaterialPayload { Material = ShopMat("Cup_Silver", new Color(0.8f, 0.82f, 0.86f), 0.8f, 0.9f) },
                Coins(coins, 2500), new RewardedAdPriceOption { AdsRequired = 5 });
            Item("cup_ruby", cupSlot, 4, new MaterialPayload { Material = ShopMat("Cup_Ruby", new Color(0.6f, 0.05f, 0.1f), 0.85f, 0.1f) },
                new PurchasePriceOption { ProductId = "cup_ruby" });

            // Скины: реклама — нефрит, покупка — обсидиан и золото.
            EnsurePurchase(ItemAt("skin_obsidian"), "skin_obsidian");
            EnsurePurchase(ItemAt("skin_gold"), "skin_gold");

            // Лампы: монеты — фонарь, реклама — латунная, покупка — хрустальная.
            Item("lamp_brass", lampSlot, 2, new MaterialPayload { Material = ShopMat("Lamp_Brass", new Color(0.72f, 0.55f, 0.25f), 0.6f, 0.8f) },
                Coins(coins, 1500), new RewardedAdPriceOption { AdsRequired = 4 });
            Item("lamp_crystal", lampSlot, 3, new MaterialPayload
                {
                    Material = ShopMat("Lamp_Crystal", new Color(0.82f, 0.9f, 1f), 0.95f, 0f, new Color(0.25f, 0.32f, 0.4f)),
                },
                new PurchasePriceOption { ProductId = "lamp_crystal" });

            // Стили броска: монеты и реклама уже есть; босс-стили можно купить, «Королевский» — только за деньги.
            EnsureCoins(ItemAt("roll_granny"), coins, 2500);
            EnsureCoins(ItemAt("roll_pirate"), coins, 3000);
            EnsurePurchase(ItemAt("roll_pirate"), "roll_pirate");
            var rollRoyal = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Royal.asset", s =>
            {
                s.ShakeDuration = new Vector2(1.1f, 1.4f);
                s.ShakeAmplitude = new Vector2(0.03f, 0.05f);
                s.ShakeFrequency = new Vector2(10f, 13f);
                s.ShakeTilt = new Vector2(18f, 26f);
                s.PourAngle = new Vector2(100f, 115f);
                s.ThrowSpeed = new Vector2(2.2f, 2.6f);
                s.SpinSpeed = new Vector2(20f, 28f);
            });
            TuneThrow(rollRoyal, 0.3f, 0.4f, 20f, 30f, 0.4f, 0.5f, 0.2f, 0.3f, 0.2f, 0.25f, 0.1f, 0.15f);
            Item("roll_royal", rollSlot, 7, new RollStylePayload { Style = rollRoyal }, new PurchasePriceOption { ProductId = "roll_royal" });

            // Пакеты монет (расходуемые товары). Рекомендуемые цены на площадке — в README.
            CoinPack("coins_small", "pack.small", coins, 2000, 0, 1);
            CoinPack("coins_medium", "pack.medium", coins, 6000, 15, 2);
            CoinPack("coins_large", "pack.large", coins, 16000, 30, 3);

            // Стартовый набор: один раз, кнопкой в главном меню, пока не куплен.
            Asset<ThemeSetConfig>(ConfigsFolder + "/Themes/Starter.asset", t =>
            {
                Identity(t, "theme_starter", "theme.starter");
                t.DescriptionKey = "theme.starter.desc";
                t.Featured = true;
                t.Order = 0;
                t.Items = new List<CosmeticItemConfig> { ItemAt("cup_wood"), ItemAt("felt_red"), ItemAt("table_dark") };
                t.Dice = new List<DieConfig> { lucky };
                t.Rewards = new List<Reward>
                {
                    new CurrencyReward { Currency = coins, Amount = 5000 },
                    new CurrencyReward { Currency = energy, Amount = 10 },
                };
                t.Price.Options.Add(new PurchasePriceOption { ProductId = "starter_pack" });
            });

            // «Без рекламы»: право шаблона no_ads (убирает межстраничную рекламу, награды за рекламу остаются).
            Asset<ThemeSetConfig>(ConfigsFolder + "/Themes/NoAds.asset", t =>
            {
                Identity(t, "theme_no_ads", "theme.noAds");
                t.DescriptionKey = "theme.noAds.desc";
                t.Order = 1;
                t.Price.Options.Add(new PurchasePriceOption { ProductId = Base.Services.Monetization.EntitlementIds.NoAds });
            });

            SetThemeInfo("theme_pirate", "theme.pirate.desc", 3);
            SetThemeInfo("theme_dice_pack", "theme.dicePack.desc", 2);
            SetStakePayouts();
        }

        /// <summary>
        /// Выплата ставки по силе соперника: победы ИИ против «среднего игрока» (Zonk/Balance Simulator) переводятся
        /// в честный коэффициент p/(1−p) с запасом 10%, чтобы ставки на слабых не стали фермой монет.
        /// </summary>
        private static void SetStakePayouts()
        {
            var payouts = new Dictionary<string, float>
            {
                { "ai_chaotic", 0.4f }, { "ai_novice", 0.6f }, { "ai_greedy", 0.65f },
                { "ai_cautious", 0.7f }, { "ai_balanced", 0.9f }, { "ai_expert", 1f },
            };

            foreach (var guid in AssetDatabase.FindAssets("t:OpponentConfig", new[] { ConfigsFolder }))
            {
                var opponent = AssetDatabase.LoadAssetAtPath<OpponentConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (opponent == null || opponent.Ai == null || !Mathf.Approximately(opponent.StakePayout, 1f) ||
                    !payouts.TryGetValue(opponent.Ai.Id, out var payout))
                {
                    continue;
                }

                // Боссы сильнее профиля: особые кости и правило.
                opponent.StakePayout = opponent.IsBoss ? payout + 0.1f : payout;
                EditorUtility.SetDirty(opponent);
            }
        }

        private static Material ShopMat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            return Mat(new ArtSet(), name, color, null, smoothness, metallic, emission ?? Color.black);
        }

        private static CosmeticSlotConfig SlotAt(string id)
        {
            return AssetDatabase.LoadAssetAtPath<CosmeticSlotConfig>(ConfigsFolder + "/Cosmetics/Slots/Slot_" + id + ".asset");
        }

        private static void CoinPack(string productId, string nameKey, CurrencyConfig coins, int amount, int bonus, int order)
        {
            Asset<CoinPackConfig>(ConfigsFolder + "/CoinPacks/" + productId + ".asset", p =>
            {
                Identity(p, "pack_" + productId, nameKey);
                p.ProductId = productId;
                p.Currency = coins;
                p.Amount = amount;
                p.BonusPercent = bonus;
                p.Order = order;
            });
        }

        private static void SetThemeInfo(string id, string descriptionKey, int order)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ThemeSetConfig", new[] { ConfigsFolder }))
            {
                var theme = AssetDatabase.LoadAssetAtPath<ThemeSetConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme == null || theme.Id != id || !string.IsNullOrEmpty(theme.DescriptionKey))
                    continue;

                theme.DescriptionKey = descriptionKey;
                theme.Order = order;
                EditorUtility.SetDirty(theme);
            }
        }

        /// <summary>Вариант «купить за деньги», если у предмета его ещё нет.</summary>
        private static void EnsurePurchase(CosmeticItemConfig item, string productId)
        {
            if (item == null || item.Price.Options.Exists(o => o is PurchasePriceOption))
                return;

            item.Price.Options.Add(new PurchasePriceOption { ProductId = productId });
            EditorUtility.SetDirty(item);
        }

        /// <summary>Вариант «за монеты», если у предмета его ещё нет.</summary>
        private static void EnsureCoins(CosmeticItemConfig item, CurrencyConfig coins, int amount)
        {
            if (item == null || item.Price.Options.Exists(o => o is CurrencyPriceOption))
                return;

            item.Price.Options.Add(Coins(coins, amount));
            EditorUtility.SetDirty(item);
        }
    }
}
