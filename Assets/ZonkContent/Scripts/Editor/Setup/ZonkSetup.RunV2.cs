using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Core.Modifiers;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// «Бесконечный забег 2.0» (02.10.2026): гружёные кости забега (RunOnly), пул находок с редкостью, стаками и тактиками,
    /// перенастройка плана этажей под новую кривую (один раз, если в ассете старые числа), новые рубежи, окно выбора
    /// с подзаголовком и дополнительными кнопками; башня — ещё 30 этажей с главами 5–10 (если в ней старые 30).
    /// Уже настроенное руками не затирается: пул находок заполняется, только если пуст.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static void BuildRunV2(GameConfig config, EndlessRunConfig run, TowerConfig tower)
        {
            if (run != null)
            {
                var steps = BuildLoadedDice();
                EnsureRunPerks(run, steps);
                RetuneRunPlanV2(run);
                EnsureRunMilestones(run, config);
            }

            if (tower != null)
                ExtendTowerV2(tower, config);
        }

        /// <summary>Гружёные кости: «1 или 5» → «всегда 5» → «всегда 1». Только для забега, честно: веса граней.</summary>
        private static List<DieConfig> BuildLoadedDice()
        {
            var result = new List<DieConfig>();
            foreach (var (id, weights, marker) in new[]
                     {
                         ("loaded15", new[] { 1f, 0f, 0f, 0f, 1f, 0f }, new Color(0.95f, 0.75f, 0.2f)),
                         ("loaded5", new[] { 0f, 0f, 0f, 0f, 1f, 0f }, new Color(1f, 0.55f, 0.15f)),
                         ("loaded1", new[] { 1f, 0f, 0f, 0f, 0f, 0f }, new Color(1f, 0.3f, 0.2f)),
                     })
            {
                var die = Asset<DieConfig>(ConfigsFolder + "/Dice/Die_" + id + ".asset", d =>
                {
                    Identity(d, "die_" + id, "die." + id);
                    d.DescriptionKey = "die." + id + ".desc";
                    d.Weights = weights;
                    d.MarkerColor = marker;
                    d.RunOnly = true;
                    d.Price.Options.Add(new ProgressPriceOption { HintKey = "hint.runOnly" });
                });
                if (!die.RunOnly)
                {
                    die.RunOnly = true;
                    EditorUtility.SetDirty(die);
                }

                result.Add(die);
            }

            return result;
        }

        /// <summary>Пул находок по тактикам: веса и стаки подобраны симулятором (README, журнал 2026-10-02).</summary>
        private static void EnsureRunPerks(EndlessRunConfig run, List<DieConfig> loaded)
        {
            if (run.Perks.Count > 0)
                return;

            void Add(string id, Rarity rarity, int weight, int max, RunTactic tactic, RunPerk perk)
                => run.Perks.Add(new RunPerkEntry { Id = id, Rarity = rarity, Weight = weight, MaxStacks = max, Tactic = tactic, Perk = perk });

            Add("heart", Rarity.Common, 3, 99, RunTactic.General, new HeartPerk());
            Add("combo", Rarity.Common, 10, 99, RunTactic.Risky, new ComboPerk { Step = 0.2f });
            Add("die", Rarity.Common, 6, 6, RunTactic.Dice, new SpecialDiePerk());
            Add("loaded", Rarity.Rare, 3, 3, RunTactic.Dice, new LoadedDiePerk { Steps = loaded });
            Add("shield", Rarity.Rare, 3, 99, RunTactic.Careful, new ShieldPerk());
            Add("head_start", Rarity.Common, 5, 3, RunTactic.Careful, new HeadStartPerk { Points = 150 });
            Add("insurance", Rarity.Rare, 4, 3, RunTactic.Careful, new InsurancePerk { PercentPerStack = 15, MaxPercent = 45 });
            Add("relief", Rarity.Rare, 3, 3, RunTactic.Careful, new ReliefPerk { PercentPerStack = 8, MaxPercent = 24 });
            Add("hot_hand", Rarity.Common, 6, 3, RunTactic.Risky, new HotHandPerk { PercentPerStack = 50 });
            Add("big_turn", Rarity.Common, 6, 3, RunTactic.Risky, new BigTurnPerk { Threshold = 1000, PercentPerStack = 15 });
            Add("single_one", Rarity.Common, 5, 3, RunTactic.Risky, new SingleFacePerk { Face = 1, StepPerStack = 0.3f });
            Add("single_five", Rarity.Common, 5, 3, RunTactic.Risky, new SingleFacePerk { Face = 5, StepPerStack = 0.3f });
            Add("zonk_save", Rarity.Rare, 4, 99, RunTactic.Lucky, new ZonkSavePerk { Charges = 1 });
            Add("lucky_charm", Rarity.Legendary, 1, 1, RunTactic.Lucky, new LuckyCharmPerk());
            Add("second_wind", Rarity.Legendary, 1, 1, RunTactic.Lucky, new SecondWindPerk());
            Add("golden_vein", Rarity.Legendary, 2, 1, RunTactic.General, new GoldenVeinPerk { Multiplier = 2f });
            Add("token_purse", Rarity.Common, 4, 99, RunTactic.General, new TokenPursePerk { Tokens = 5 });
            Add("token_hunter", Rarity.Rare, 2, 1, RunTactic.General, new TokenHunterPerk { Multiplier = 2f });
            Add("wider_choice", Rarity.Rare, 2, 1, RunTactic.General, new WiderChoicePerk());
            Add("veteran", Rarity.Legendary, 1, 1, RunTactic.General, new VeteranPerk());
            EditorUtility.SetDirty(run);
        }

        /// <summary>План этажей под новую кривую — если в ассете ещё числа 2026-09-30 (ручную перенастройку не трогаем).</summary>
        private static void RetuneRunPlanV2(EndlessRunConfig run)
        {
            var plan = run.Plan;
            var old = plan.TargetPerFloor == 100 && plan.TargetMax == 4000 && Mathf.Approximately(plan.EnemyStartPower, 0.8f) &&
                      Mathf.Approximately(plan.EnemyPowerPerFloor, 0.02f) && plan.EnemyDiceEvery == 6 && plan.EarlyUntil == 10 &&
                      plan.MidUntil == 30;
            if (!old)
                return;

            var fresh = new Zonk.Core.Modes.EndlessRunPlan();
            plan.TargetBase = fresh.TargetBase;
            plan.TargetPerFloor = fresh.TargetPerFloor;
            plan.TargetMax = fresh.TargetMax;
            plan.EnemyStartPower = fresh.EnemyStartPower;
            plan.EnemyPowerPerFloor = fresh.EnemyPowerPerFloor;
            plan.EnemyPowerAccel = fresh.EnemyPowerAccel;
            plan.EnemyDiceEvery = fresh.EnemyDiceEvery;
            plan.EarlyUntil = fresh.EarlyUntil;
            plan.MidUntil = fresh.MidUntil;
            EditorUtility.SetDirty(run);
            Debug.Log("[Setup] Endless run plan v2 applied: soft start, accelerating enemy power");
        }

        /// <summary>Рубежи 5 / 15 / 20 / 30 — если их нет (10 / 25 / 50 / 100 уже были).</summary>
        private static void EnsureRunMilestones(EndlessRunConfig run, GameConfig config)
        {
            var changed = false;
            foreach (var (floor, coins, energy) in new[] { (5, 100, 0), (15, 300, 2), (20, 400, 3), (30, 800, 4) })
            {
                if (run.Milestones.Exists(m => m != null && m.Floor == floor))
                    continue;
                var milestone = new RunMilestone { Floor = floor };
                if (config.Coins != null)
                    milestone.Rewards.Add(Gift(config.Coins, coins));
                if (energy > 0 && config.Energy != null)
                    milestone.Rewards.Add(Gift(config.Energy, energy));
                // TODO: вещь «за игру» на рубеж 25 / 50 (рамка «Глубина», стакан «Бездна») — когда появятся модели.
                run.Milestones.Add(milestone);
                changed = true;
            }

            if (!changed)
                return;
            run.Milestones.Sort((a, b) => a.Floor.CompareTo(b.Floor));
            EditorUtility.SetDirty(run);
        }

        /// <summary>
        /// Башня 30 → 60 этажей: главы 5–10 по отрезкам (4 соперника и страж-босс главы, на вершине — Грозная Фортуна),
        /// в каждом отрезке новое общее правило. Новые этажи — в конец: игроки продолжают с рубежа.
        /// </summary>
        private static void ExtendTowerV2(TowerConfig tower, GameConfig config)
        {
            if (tower.Floors.Count != 30)
                return;

            var segments = new[]
            {
                (regulars: new[] { "foma", "miron", "lili", "alfred" }, guardian: "rose", target: 7000,
                    rules: new MatchModifier[] { new EntryScoreModifier { EntryScore = 500 } }),
                (regulars: new[] { "barsik", "agrippina", "karl", "serafim" }, guardian: "tikhon", target: 7500,
                    rules: new MatchModifier[] { new MinBankModifier { MinBankScore = 350 } }),
                (regulars: new[] { "twins", "glyba", "voldemar", "nina" }, guardian: "krot", target: 8000,
                    rules: new MatchModifier[] { new ThreeZonkPenaltyModifier { Penalty = 500 } }),
                (regulars: new[] { "erofey", "aglaya", "gordeev", "stavkin" }, guardian: "madame", target: 8500,
                    rules: new MatchModifier[] { new ZonkPenaltyModifier { Penalty = 200 } }),
                (regulars: new[] { "vetrov", "zhemchugov", "marta", "strogiy" }, guardian: "sych", target: 9000,
                    rules: new MatchModifier[] { new SingleFaceModifier { Face = 5, Multiplier = 0f } }),
                (regulars: new[] { "jester", "karr", "sudba", "arkhip" }, guardian: "fortuna_dread", target: 10000,
                    rules: new MatchModifier[] { new OpponentStartsModifier(), new ZonkPenaltyModifier { Penalty = 200 } }),
            };

            for (var s = 0; s < segments.Length; s++)
            {
                var segment = segments[s];
                foreach (var id in segment.regulars)
                {
                    var number = tower.Floors.Count + 1;
                    var floor = new TowerFloor { Opponent = OpponentAt(id), Target = Mathf.Min(6000, 2000 + 100 * number) };
                    foreach (var rule in segment.rules)
                        floor.Rules.Add(CopyRule(rule));
                    if (config.Coins != null)
                        floor.FirstClearRewards.Add(Gift(config.Coins, 20 + 5 * number));
                    tower.Floors.Add(floor);
                }

                var guardian = new TowerFloor { Opponent = OpponentAt(segment.guardian), Target = segment.target };
                if (config.Coins != null)
                    guardian.FirstClearRewards.Add(Gift(config.Coins, 150 * (s + 7)));
                if (config.Energy != null)
                    guardian.FirstClearRewards.Add(Gift(config.Energy, 3));
                tower.Floors.Add(guardian);
            }

            if (config.Coins != null)
                tower.Floors[tower.Floors.Count - 1].FirstClearRewards.Add(Gift(config.Coins, 4000));
            EditorUtility.SetDirty(tower);
            Debug.Log("[Setup] Tower extended to " + tower.Floors.Count + " floors (chapters 5–10)");
        }

        /// <summary>Окно выбора находок: строка под заголовком и ряд дополнительных кнопок внизу — в готовый префаб.</summary>
        private static void UpgradePerkChoiceWindow()
        {
            EditWindowPrefab<PerkChoiceWindow>("PerkChoiceWindow", "_subtitle", (root, window) =>
            {
                var options = new SerializedObject(window).FindProperty("_options").objectReferenceValue as RectTransform;
                var panel = options != null ? options.parent : root;
                var subtitle = Text("Subtitle", panel, _ui.Font, 28, _ui.Palette.TextMuted);
                AutoSize(subtitle, 16, 28);
                Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -150f), new Vector2(-80f, 50f));
                if (options != null)
                    options.offsetMax = new Vector2(options.offsetMax.x, -190f);
                return subtitle;
            });

            EditWindowPrefab<PerkChoiceWindow>("PerkChoiceWindow", "_extras", (root, window) =>
            {
                var options = new SerializedObject(window).FindProperty("_options").objectReferenceValue as RectTransform;
                var panel = options != null ? options.parent : root;
                var extras = Row((RectTransform)panel, 24);
                extras.name = "Extras";
                extras.anchorMin = new Vector2(0f, 0f);
                extras.anchorMax = new Vector2(1f, 0f);
                extras.pivot = new Vector2(0.5f, 0f);
                extras.offsetMin = new Vector2(120f, 24f);
                extras.offsetMax = new Vector2(-120f, 104f);
                if (options != null)
                    options.offsetMin = new Vector2(options.offsetMin.x, 130f);
                return extras;
            });
        }
    }
}
