using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Режимы-испытания (2026-09-30): «Бесконечный забег» (Configs/Modes/EndlessRun.asset) и башня
    /// (Configs/Modes/Tower.asset, 30 этажей), таблицы рекордов для них. Создаются один раз, дальше правятся руками;
    /// новые этажи башни — строками в Tower.asset.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string ModesFolder = ConfigsFolder + "/Modes";

        private static void BuildModes(GameConfig config)
        {
            EnsureFolder(ModesFolder);
            var coins = config.Coins;
            var energy = config.Energy;

            var run = Asset<EndlessRunConfig>(ModesFolder + "/EndlessRun.asset", r =>
            {
                foreach (var id in new[] { "vitya", "klava", "petrovich", "semenych", "lutik", "gustav", "irma", "zhora",
                             "stepan", "zina", "max", "efim", "pit", "bart", "greta", "hook" })
                    AddIfFound(r.Opponents, OpponentAt(id));

                // Стражи по кругу: боссы, затем их грозные версии.
                foreach (var id in new[] { "agafya", "bo", "claw", "captain", "agafya_dread", "bo_dread", "claw_dread", "captain_dread" })
                    AddIfFound(r.Guardians, OpponentAt(id));

                r.AiEarly = AiAt("novice");
                r.AiMid = AiAt("balanced");
                r.AiLate = AiAt("expert");
                foreach (var id in new[] { "worn", "middle", "bone", "fives", "even", "odd", "edges", "sixes", "lucky", "sharper" })
                    AddIfFound(r.SpecialDice, DieAt(id));

                r.RulePool = new List<MatchModifier>
                {
                    new ThreeZonkPenaltyModifier { Penalty = 500 },
                    new ZonkPenaltyModifier { Penalty = 200 },
                    new SingleFaceModifier { Face = 5, Multiplier = 0f },
                    new ComboMultiplierModifier { Category = ComboCategory.OfAKind, Multiplier = 2f },
                    new ComboMultiplierModifier { Category = ComboCategory.Straight, Multiplier = 2f },
                    new MinBankModifier { MinBankScore = 350 },
                    new ComboMultiplierModifier { Category = ComboCategory.ThreePairs, Multiplier = 2f },
                    new EntryScoreModifier { EntryScore = 500 },
                };

                r.Milestones = new List<RunMilestone>
                {
                    Milestone(10, Gift(coins, 200)),
                    Milestone(25, Gift(coins, 500), Gift(energy, 3)),
                    Milestone(50, Gift(coins, 1500), Gift(energy, 5)),
                    Milestone(100, Gift(coins, 5000)),
                };
            });

            var tower = Asset<TowerConfig>(ModesFolder + "/Tower.asset", t => t.Floors = TowerFloors(coins, energy));

            if (config.EndlessRun == null || config.Tower == null)
            {
                config.EndlessRun = config.EndlessRun != null ? config.EndlessRun : run;
                config.Tower = config.Tower != null ? config.Tower : tower;
                EditorUtility.SetDirty(config);
            }

            Leaderboard("endless", "leaderboard.endless", "zonkEndless", LeaderboardMetric.EndlessRunFloor, 3);
            Leaderboard("tower", "leaderboard.tower", "zonkTower", LeaderboardMetric.TowerFloor, 4);
        }

        /// <summary>
        /// 30 этажей: рубеж каждые 5 — страж (боссы, на 25-м и 30-м грозные версии), между ними обычные соперники
        /// по возрастанию силы; цель растёт, в каждом отрезке добавляется общее правило.
        /// </summary>
        private static List<TowerFloor> TowerFloors(CurrencyConfig coins, CurrencyConfig energy)
        {
            var segments = new[]
            {
                (regulars: new[] { "vitya", "klava", "petrovich", "semenych" }, guardian: "agafya", guardianTarget: 5000,
                    rules: new MatchModifier[0]),
                (regulars: new[] { "lutik", "gustav", "irma", "zhora" }, guardian: "bo", guardianTarget: 6000,
                    rules: new MatchModifier[] { new ThreeZonkPenaltyModifier { Penalty = 500 } }),
                (regulars: new[] { "stepan", "zina", "max", "efim" }, guardian: "claw", guardianTarget: 7000,
                    rules: new MatchModifier[] { new MinBankModifier { MinBankScore = 300 } }),
                (regulars: new[] { "pit", "bart", "greta", "hook" }, guardian: "captain", guardianTarget: 8000,
                    rules: new MatchModifier[] { new ZonkPenaltyModifier { Penalty = 200 } }),
                (regulars: new[] { "zhora", "efim", "greta", "hook" }, guardian: "agafya_dread", guardianTarget: 10000,
                    rules: new MatchModifier[] { new SingleFaceModifier { Face = 5, Multiplier = 0f } }),
                (regulars: new[] { "irma", "zina", "bart", "hook" }, guardian: "captain_dread", guardianTarget: 12000,
                    rules: new MatchModifier[]
                    {
                        new ComboMultiplierModifier { Category = ComboCategory.OfAKind, Multiplier = 2f },
                        new ZonkPenaltyModifier { Penalty = 200 },
                    }),
            };

            var floors = new List<TowerFloor>();
            for (var s = 0; s < segments.Length; s++)
            {
                var segment = segments[s];
                foreach (var id in segment.regulars)
                {
                    var number = floors.Count + 1;
                    var floor = new TowerFloor { Opponent = OpponentAt(id), Target = 2000 + 100 * number };
                    foreach (var rule in segment.rules)
                        floor.Rules.Add(CopyRule(rule));
                    floor.FirstClearRewards.Add(Gift(coins, 20 + 5 * number));
                    floors.Add(floor);
                }

                var guardian = new TowerFloor { Opponent = OpponentAt(segment.guardian), Target = segment.guardianTarget };
                guardian.FirstClearRewards.Add(Gift(coins, 150 * (s + 1)));
                guardian.FirstClearRewards.Add(Gift(energy, 2));
                floors.Add(guardian);
            }

            // Вершина — большая награда за всю башню.
            floors[floors.Count - 1].FirstClearRewards.Add(Gift(coins, 2000));
            return floors;
        }

        private static RunMilestone Milestone(int floor, params Reward[] rewards)
        {
            return new RunMilestone { Floor = floor, Rewards = new List<Reward>(rewards) };
        }

        private static OpponentConfig OpponentAt(string id)
        {
            return AssetDatabase.LoadAssetAtPath<OpponentConfig>(ConfigsFolder + "/Opponents/Opp_" + id + ".asset");
        }

        private static DieConfig DieAt(string id)
        {
            return AssetDatabase.LoadAssetAtPath<DieConfig>(ConfigsFolder + "/Dice/Die_" + id + ".asset");
        }

        private static AiProfileConfig AiAt(string id)
        {
            return AssetDatabase.LoadAssetAtPath<AiProfileConfig>(ConfigsFolder + "/Ai/Ai_" + id + ".asset");
        }

        private static void AddIfFound<T>(List<T> list, T item) where T : Object
        {
            if (item != null && !list.Contains(item))
                list.Add(item);
        }
    }
}
