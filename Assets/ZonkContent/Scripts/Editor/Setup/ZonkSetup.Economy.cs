using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Перенастройка экономики в уже созданных ассетах. Генератор не затирает ручные правки, поэтому число меняется,
    /// только если в ассете всё ещё стоит прежнее значение генератора (from). Новые значения те же, что при создании
    /// ассетов в ZonkSetup.Content. Проверить итог: окно Zonk/Economy Report.
    /// Следующая перенастройка — новая таблица ниже (v3 …), старые не удалять: по ним обновляются старые проекты.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string CoinsId = "coins";

        /// <summary>v2 (2026-09-29): цены за монеты выше: кости примерно вдвое, косметика в 2.5–4 раза (долгая цель).</summary>
        private static readonly (string id, int from, int to)[] CoinPricesV2 =
        {
            ("env_tavern", 1500, 4000), ("env_beach", 2000, 6000), ("env_ship", 3000, 10000),
            ("table_dark", 400, 1200), ("table_marble", 700, 2500), ("felt_red", 250, 800),
            ("cup_wood", 300, 1000), ("cup_gold", 900, 4000),
            ("skin_ruby", 300, 1000), ("skin_obsidian", 600, 2000), ("skin_gold", 1200, 5000),
            ("lamp_lantern", 350, 1200), ("roll_calm", 300, 1000), ("roll_quick", 300, 1000), ("roll_wild", 500, 1800),
            ("theme_pirate", 4000, 11000),
            ("die_worn", 400, 800), ("die_middle", 400, 800), ("die_bone", 600, 1200), ("die_fives", 600, 1200),
            ("die_even", 800, 1600), ("die_sixes", 1500, 3000), ("die_lucky", 1500, 3000), ("die_sharper", 2000, 4000),
        };

        /// <summary>v2: монеты за повторную победу вдвое меньше (первая победа без изменений).</summary>
        private static readonly (string id, int from, int to)[] RepeatWinsV2 =
        {
            ("opp_vitya", 20, 10), ("opp_klava", 20, 10), ("opp_petrovich", 25, 12), ("opp_semenych", 25, 12),
            ("opp_agafya", 60, 30), ("opp_lutik", 30, 15), ("opp_gustav", 30, 15), ("opp_irma", 35, 18),
            ("opp_zhora", 40, 20), ("opp_bo", 80, 40), ("opp_stepan", 45, 22), ("opp_zina", 45, 22),
            ("opp_max", 50, 25), ("opp_efim", 50, 25), ("opp_claw", 100, 50), ("opp_pit", 55, 28),
            ("opp_bart", 55, 28), ("opp_greta", 60, 30), ("opp_hook", 60, 30), ("opp_captain", 120, 60),
        };

        /// <summary>v2: монеты за задания. Задания «с костью» — по префиксу ID.</summary>
        private static readonly (string id, int from, int to)[] QuestRewardsV2 =
        {
            ("quest_play", 80, 50), ("quest_win", 120, 80), ("quest_bank", 100, 60), ("quest_big_turn", 120, 80),
            ("quest_hot_dice", 120, 80), ("quest_straight", 150, 100), ("quest_of_a_kind", 100, 60),
            ("quest_plain_win", 150, 100), ("quest_shop", 100, 60), ("quest_win_with_*", 150, 100),
            ("quest_watch_ads", 120, 80), ("quest_watch_ads_week", 700, 400), ("quest_visit_days", 500, 300),
            ("quest_claim_dailies", 600, 400), ("quest_win_week", 500, 300), ("quest_bosses", 400, 300),
            ("quest_bank_week", 400, 250), ("quest_hot_dice_week", 400, 250),
        };

        /// <summary>v2: мастерство дольше (очки уровней) и скромнее (монеты за уровень).</summary>
        private static readonly (int fromPoints, int toPoints, int fromCoins, int toCoins)[] MasteryV2 =
        {
            (1500, 5000, 100, 50), (6000, 25000, 250, 150), (20000, 80000, 500, 300),
        };

        /// <summary>
        /// v2: обычные соперники глав 3–4 больше не дарят косметику (только монеты), Капитан — золотой стакан
        /// (играет им, но не отдаёт). Косметика — цель для монет, рекламы, покупок и боссов.
        /// </summary>
        private static readonly (string opponent, string item)[] RemovedRewardsV2 =
        {
            ("opp_stepan", "felt_blue"), ("opp_zina", "skin_jade"), ("opp_max", "roll_quick"), ("opp_efim", "lamp_lantern"),
            ("opp_pit", "skin_ruby"), ("opp_bart", "table_dark"), ("opp_greta", "skin_obsidian"), ("opp_hook", "roll_showman"),
            ("opp_captain", "cup_gold"),
        };

        private static void RetuneEconomy(GameConfig config)
        {
            var content = new Dictionary<string, ContentConfig>();
            foreach (var guid in AssetDatabase.FindAssets("t:ContentConfig", new[] { ConfigsFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ContentConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !string.IsNullOrEmpty(asset.Id))
                    content[asset.Id] = asset;
            }

            var changed = 0;
            foreach (var (id, from, to) in CoinPricesV2)
            {
                if (content.TryGetValue(id, out var item) && RetunePrice(Pricing.PriceOf(item), from, to))
                    changed += Dirty(item);
            }

            foreach (var (id, from, to) in RepeatWinsV2)
            {
                if (content.TryGetValue(id, out var item) && item is OpponentConfig opponent &&
                    RetuneRewards(opponent.RepeatWinRewards, from, to))
                {
                    changed += Dirty(opponent);
                }
            }

            foreach (var (opponentId, itemId) in RemovedRewardsV2)
            {
                if (content.TryGetValue(opponentId, out var item) && item is OpponentConfig opponent &&
                    opponent.FirstWinRewards.RemoveAll(r => r is ContentReward reward && reward.Item != null && reward.Item.Id == itemId) > 0)
                {
                    changed += Dirty(opponent);
                }
            }

            foreach (var (id, from, to) in QuestRewardsV2)
            {
                foreach (var pair in content)
                {
                    var matches = id.EndsWith("*") ? pair.Key.StartsWith(id.TrimEnd('*')) : pair.Key == id;
                    if (matches && pair.Value is QuestConfig quest && RetuneRewards(quest.Rewards, from, to))
                        changed += Dirty(quest);
                }
            }

            if (config != null && config.MasteryLevels.Count == MasteryV2.Length)
            {
                var untouched = true;
                for (var i = 0; i < MasteryV2.Length; i++)
                    untouched &= config.MasteryLevels[i] != null && config.MasteryLevels[i].Points == MasteryV2[i].fromPoints;

                if (untouched)
                {
                    for (var i = 0; i < MasteryV2.Length; i++)
                    {
                        config.MasteryLevels[i].Points = MasteryV2[i].toPoints;
                        RetuneRewards(config.MasteryLevels[i].Rewards, MasteryV2[i].fromCoins, MasteryV2[i].toCoins);
                    }

                    changed += Dirty(config);
                }
            }

            if (changed > 0)
                Debug.Log($"[Setup] Economy v2 applied to {changed} assets");
        }

        /// <summary>Цена за монеты from → to, если стоит прежнее значение.</summary>
        private static bool RetunePrice(Price price, int from, int to)
        {
            if (price == null)
                return false;

            var changed = false;
            foreach (var option in price.Options)
            {
                if (option is CurrencyPriceOption currency && currency.Currency != null && currency.Currency.Id == CoinsId &&
                    currency.Amount == from)
                {
                    currency.Amount = to;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Награда в монетах from → to, если стоит прежнее значение.</summary>
        private static bool RetuneRewards(List<Reward> rewards, int from, int to)
        {
            var changed = false;
            foreach (var reward in rewards)
            {
                if (reward is CurrencyReward currency && currency.Currency != null && currency.Currency.Id == CoinsId &&
                    currency.Amount == from)
                {
                    currency.Amount = to;
                    changed = true;
                }
            }

            return changed;
        }

        private static int Dirty(Object asset)
        {
            EditorUtility.SetDirty(asset);
            return 1;
        }
    }
}
