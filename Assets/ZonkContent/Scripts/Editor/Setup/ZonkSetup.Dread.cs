using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Грозные версии боссов (2026-09-30). Тот же персонаж, но свой конфиг: правила босса плюс ещё одно, 5–6 особых
    /// костей, ИИ «эксперт», цель 12000, свои звёзды и уникальный вид костей в награду (босс сам играет этим видом —
    /// игрок видит, за что борется). Открываются тремя звёздами у обычного босса, лежат в ChapterConfig.DreadBosses.
    ///
    /// Подобрано симулятором против «среднего игрока» Balance Simulator: босс побеждает 64–68% партий (обычные боссы
    /// на 10000 — 61–64%). Сильнее честно не сделать: правила действуют на обоих, особые кости сбалансированы.
    /// Звёзды — около 35–50% побед игрока каждая. Уже созданные ассеты генератор не трогает.
    /// </summary>
    public static partial class ZonkSetup
    {
        private sealed class DreadSpec
        {
            public string BossId;
            public string ChapterAsset;
            public string SkinId;
            public int SkinOrder;
            public string[] Dice;
            public MatchModifier[] ExtraRules;
            public StarCondition[] Stars;
            public int FirstCoins;
            public int RepeatCoins;
            public Color Tint;
        }

        private const int DreadTarget = 12000;

        private static void BuildDread()
        {
            var specs = new[]
            {
                new DreadSpec
                {
                    BossId = "agafya", ChapterAsset = "Ch1_Home", SkinId = "skin_dread_agafya", SkinOrder = 20,
                    Dice = new[] { "lucky", "sharper", "worn", "edges", "bone" },
                    ExtraRules = new MatchModifier[] { new ZonkPenaltyModifier { Penalty = 200 } },
                    Stars = new StarCondition[] { new MaxZonksStar { Zonks = 3 }, new WinByMarginStar { Margin = 3000 } },
                    FirstCoins = 400, RepeatCoins = 50, Tint = new Color(0.7f, 0.4f, 0.85f),
                },
                new DreadSpec
                {
                    BossId = "bo", ChapterAsset = "Ch2_Tavern", SkinId = "skin_dread_bo", SkinOrder = 21,
                    Dice = new[] { "sharper", "lucky", "edges", "odd", "sixes", "worn" },
                    ExtraRules = new MatchModifier[] { new MinBankModifier { MinBankScore = 300 } },
                    Stars = new StarCondition[] { new MaxTurnsStar { Turns = 12 }, new BigTurnStar { Points = 3000 } },
                    FirstCoins = 500, RepeatCoins = 60, Tint = new Color(0.8f, 0.25f, 0.22f),
                },
                new DreadSpec
                {
                    BossId = "claw", ChapterAsset = "Ch3_Beach", SkinId = "skin_dread_claw", SkinOrder = 22,
                    Dice = new[] { "lucky", "sharper", "edges", "even", "odd", "sixes" },
                    ExtraRules = new MatchModifier[] { new ZonkPenaltyModifier { Penalty = 200 } },
                    Stars = new StarCondition[] { new NoSpecialDiceStar(), new WinByMarginStar { Margin = 3500 } },
                    FirstCoins = 600, RepeatCoins = 70, Tint = new Color(1f, 0.45f, 0.35f),
                },
                new DreadSpec
                {
                    BossId = "captain", ChapterAsset = "Ch4_Ship", SkinId = "skin_dread_captain", SkinOrder = 23,
                    Dice = new[] { "sharper", "lucky", "edges", "odd", "worn", "sixes" },
                    ExtraRules = new MatchModifier[] { new ZonkPenaltyModifier { Penalty = 300 } },
                    Stars = new StarCondition[] { new MaxTurnsStar { Turns = 18 }, new WinByMarginStar { Margin = 4000 } },
                    FirstCoins = 700, RepeatCoins = 80, Tint = new Color(0.45f, 0.42f, 0.5f),
                },
            };

            var coins = AssetDatabase.LoadAssetAtPath<CurrencyConfig>(ConfigsFolder + "/Currencies/Coins.asset");
            var expert = AssetDatabase.LoadAssetAtPath<AiProfileConfig>(ConfigsFolder + "/Ai/Ai_expert.asset");
            var skinSlot = SlotAt("dice_skin");

            foreach (var spec in specs)
            {
                var boss = AssetDatabase.LoadAssetAtPath<OpponentConfig>(ConfigsFolder + "/Opponents/Opp_" + spec.BossId + ".asset");
                var chapter = AssetDatabase.LoadAssetAtPath<ChapterConfig>(ConfigsFolder + "/Chapters/" + spec.ChapterAsset + ".asset");
                if (boss == null || chapter == null || skinSlot == null)
                {
                    Debug.LogWarning($"[Setup] Dread {spec.BossId}: boss, chapter or dice skin slot not found, skipped");
                    continue;
                }

                // Уникальный вид костей: только награда за первую победу над грозной версией.
                var material = DreadDieMaterial(spec.SkinId, spec.Tint);
                var skin = Item(spec.SkinId, skinSlot, spec.SkinOrder, new MeshMaterialPayload { Material = material },
                    new ProgressPriceOption { HintKey = "hint." + spec.SkinId.Replace("skin_", string.Empty) });

                var id = spec.BossId + "_dread";
                var dread = Asset<OpponentConfig>(ConfigsFolder + "/Opponents/Opp_" + id + ".asset", o =>
                {
                    Identity(o, "opp_" + id, "opp." + id);
                    o.TitleKey = "opp." + id + ".title";
                    o.DreadOf = boss;
                    o.DreadUnlockStars = 3;
                    o.IsBoss = true;
                    o.Ai = expert != null ? expert : boss.Ai;
                    o.Reactions = boss.Reactions;
                    o.BodyColor = boss.BodyColor * 0.6f + new Color(0.3f, 0f, 0f, 0.4f);
                    o.Accessory = boss.Accessory;
                    o.Cup = boss.Cup;
                    o.DiceSkin = skin;
                    o.RollStyle = boss.RollStyle;
                    o.StakePayout = boss.StakePayout;
                    o.TargetScore = DreadTarget;

                    o.Dice = new List<DieConfig>();
                    foreach (var dieId in spec.Dice)
                    {
                        var die = AssetDatabase.LoadAssetAtPath<DieConfig>(ConfigsFolder + "/Dice/Die_" + dieId + ".asset");
                        if (die != null)
                            o.Dice.Add(die);
                    }

                    // Правила обычного босса (копии) плюс новые: игрок видит весь список в окне кампании.
                    foreach (var rule in boss.Modifiers)
                    {
                        if (rule != null)
                            o.Modifiers.Add(CopyRule(rule));
                    }

                    o.Modifiers.AddRange(spec.ExtraRules);
                    o.StarConditions = new List<StarCondition>(spec.Stars);
                    o.FirstWinRewards.Add(new CurrencyReward { Currency = coins, Amount = spec.FirstCoins });
                    o.FirstWinRewards.Add(new ContentReward { Item = skin });
                    o.RepeatWinRewards.Add(new CurrencyReward { Currency = coins, Amount = spec.RepeatCoins });
                });

                if (!chapter.DreadBosses.Contains(dread))
                {
                    chapter.DreadBosses.Add(dread);
                    EditorUtility.SetDirty(chapter);
                }
            }
        }

        /// <summary>Портрет грозной версии — портрет её босса (свой не рисуется). После BuildPortraits.</summary>
        private static void LinkDreadPortraits()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:OpponentConfig", new[] { ConfigsFolder }))
            {
                var opponent = AssetDatabase.LoadAssetAtPath<OpponentConfig>(AssetDatabase.GUIDToAssetPath(guid));
                if (opponent == null || opponent.DreadOf == null || opponent.Portrait != null || opponent.DreadOf.Portrait == null)
                    continue;

                opponent.Portrait = opponent.DreadOf.Portrait;
                EditorUtility.SetDirty(opponent);
            }
        }

        /// <summary>Копия правила через JsonUtility: у грозной версии свой экземпляр, правка не задевает обычного босса.</summary>
        private static MatchModifier CopyRule(MatchModifier rule)
        {
            return (MatchModifier)JsonUtility.FromJson(JsonUtility.ToJson(rule), rule.GetType());
        }

        private static Material DreadDieMaterial(string skinId, Color tint)
        {
            var path = Materials + "/M_Die_" + skinId + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Die_Classic.png");
            material = new Material(Shader.Find(LitShader)) { name = "M_Die_" + skinId };
            material.SetColor("_BaseColor", tint);
            if (texture != null)
                material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.8f);
            material.SetFloat("_Metallic", 0.35f);
            // Эмиссия включена, как у остальных скинов: через неё светятся метки особых костей.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
