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
        /// <summary>Предметы магазина из стаканов zonk_cups.py: ID предмета → префаб. Порядок — порядок во вкладке.</summary>
        private static readonly (string Id, string Prefab)[] GeneratedCupItems =
        {
            ("cup_barrel", "Cup_Barrel"),
            ("cup_clay", "Cup_Clay"),
            ("cup_coconut", "Cup_Coconut"),
            ("cup_stone", "Cup_Stone"),
            ("cup_bone", "Cup_Bone"),
            ("cup_goblet", "Cup_Goblet"),
            ("cup_copper", "Cup_Copper"),
        };

        private static void BuildShopCatalog(CurrencyConfig coins, CurrencyConfig energy, DieConfig lucky)
        {
            var envSlot = SlotAt("environment");
            var tableSlot = SlotAt("table");
            var traySlot = SlotAt("tray");
            var cupSlot = SlotAt("cup");
            var lampSlot = SlotAt("lamp");
            var rollSlot = SlotAt("roll_style");

            // Новые предметы-заглушки (цвет материала). Цена у всех — из раскладки магазина ниже (ShopLayoutV3):
            // в каждой вкладке 2 предмета за монеты, 2 за рекламу, 2 за покупку, у каждого один способ получения.
            var dieTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Die_Classic.png");
            Item("table_walnut", tableSlot, 3, new MaterialPayload { Material = ShopMat("Table_Walnut", new Color(0.36f, 0.22f, 0.12f), 0.45f) });
            Item("table_royal", tableSlot, 4, new MaterialPayload { Material = ShopMat("Table_Royal", new Color(0.05f, 0.35f, 0.25f), 0.85f, 0.2f) });
            Item("table_birch", tableSlot, 5, new MaterialPayload { Material = ShopMat("Table_Birch", new Color(0.86f, 0.78f, 0.62f), 0.35f) });
            Item("table_gold", tableSlot, 6, new MaterialPayload { Material = ShopMat("Table_Gold", new Color(0.95f, 0.75f, 0.3f), 0.8f, 0.85f) });

            Item("felt_purple", traySlot, 3, new MaterialPayload { Material = ShopMat("Felt_Purple", new Color(0.35f, 0.12f, 0.4f), 0.1f) });
            Item("felt_royal", traySlot, 4, new MaterialPayload { Material = ShopMat("Felt_Royal", new Color(0.06f, 0.05f, 0.07f), 0.2f) });
            Item("felt_teal", traySlot, 5, new MaterialPayload { Material = ShopMat("Felt_Teal", new Color(0.08f, 0.38f, 0.4f), 0.05f) });
            Item("felt_gold", traySlot, 6, new MaterialPayload { Material = ShopMat("Felt_Gold", new Color(0.7f, 0.52f, 0.15f), 0.3f, 0.4f) });

            // Стаканы — свои модели (Tools/Blender/zonk_cups.py, префабы из BuildArt). Перекраски кожаного стакана
            // (серебро, рубин, бронза, нефрит) заменены ими 01.10.2026: их ассеты удалены, сюда не возвращать.
            var cupOrder = 3;
            foreach (var (id, prefabName) in GeneratedCupItems)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Cups/" + prefabName + ".prefab");
                if (prefab != null)
                    Item(id, cupSlot, cupOrder, new PrefabPayload { Prefab = prefab });
                cupOrder++;
            }

            var skinSlot = SlotAt("dice_skin");
            Item("skin_pearl", skinSlot, 6, new MeshMaterialPayload
            {
                Material = Mat(new ArtSet(), "Die_Pearl", new Color(0.95f, 0.93f, 1f), dieTexture, 0.9f, 0.15f, Color.black),
            });
            Item("skin_emerald", skinSlot, 7, new MeshMaterialPayload
            {
                Material = Mat(new ArtSet(), "Die_Emerald", new Color(0.35f, 0.85f, 0.5f), dieTexture, 0.85f, 0.2f, Color.black),
            });

            Item("lamp_brass", lampSlot, 2, new MaterialPayload { Material = ShopMat("Lamp_Brass", new Color(0.72f, 0.55f, 0.25f), 0.6f, 0.8f) });
            Item("lamp_crystal", lampSlot, 3, new MaterialPayload
            {
                Material = ShopMat("Lamp_Crystal", new Color(0.82f, 0.9f, 1f), 0.95f, 0f, new Color(0.25f, 0.32f, 0.4f)),
            });
            Item("lamp_paper", lampSlot, 5, new MaterialPayload { Material = ShopMat("Lamp_Paper", new Color(0.8f, 0.2f, 0.15f), 0.2f, 0f, new Color(0.3f, 0.06f, 0.03f)) });
            Item("lamp_glass", lampSlot, 6, new MaterialPayload { Material = ShopMat("Lamp_Glass", new Color(0.2f, 0.55f, 0.3f), 0.9f, 0.1f) });

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
            Item("roll_royal", rollSlot, 7, new RollStylePayload { Style = rollRoyal });

            // «Шторм»: резкая тряска, стакан почти переворачивается, кости летят быстро и крутятся.
            var rollStorm = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Storm.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.6f, 0.9f);
                s.ShakeAmplitude = new Vector2(0.1f, 0.16f);
                s.ShakeFrequency = new Vector2(30f, 38f);
                s.ShakeTilt = new Vector2(25f, 40f);
                s.PourAngle = new Vector2(130f, 150f);
                s.DirectionJitter = 30f;
                s.ThrowSpeed = new Vector2(3.4f, 4f);
                s.SpinSpeed = new Vector2(22f, 30f);
            });
            TuneThrow(rollStorm, 0.55f, 0.75f, 35f, 50f, 0.18f, 0.24f, 0.05f, 0.1f, 0.08f, 0.1f, 0.3f, 0.4f);
            Item("roll_storm", rollSlot, 8, new RollStylePayload { Style = rollStorm });

            // Две новые локации за рекламу: чердак (тёплый вечерний свет) и сад (солнечный день).
            Item("env_attic", envSlot, 5, new PrefabPayload { Prefab = AtticPrefab(), Lighting = AtticLighting() });
            Item("env_garden", envSlot, 6, new PrefabPayload { Prefab = GardenPrefab(), Lighting = GardenLighting() });

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

            // Таблицы лидеров: технические имена должны совпадать с таблицами в консоли Яндекса.
            Leaderboard("stars", "leaderboard.stars", "zonkStars", LeaderboardMetric.TotalStars, 0);
            Leaderboard("wins", "leaderboard.wins", "zonkWins", LeaderboardMetric.CampaignWins, 1);
            Leaderboard("best_turn", "leaderboard.bestTurn", "zonkBestTurn", LeaderboardMetric.BestTurn, 2);

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

        private static void Leaderboard(string id, string nameKey, string technicalName, LeaderboardMetric metric, int order)
        {
            Asset<LeaderboardConfig>(ConfigsFolder + "/Leaderboards/" + id + ".asset", l =>
            {
                Identity(l, "board_" + id, nameKey);
                l.TechnicalName = technicalName;
                l.Metric = metric;
                l.Order = order;
            });
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

    }
}
