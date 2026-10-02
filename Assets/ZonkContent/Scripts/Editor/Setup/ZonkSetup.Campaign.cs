using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Кампания на 10 глав (2026-10-02) и кривая сложности. Подобрано симулятором (стенд «прокси-игрок»:
    /// профиль ИИ balanced с обычными костями — «средний игрок»): доля побед игрока падает от 69% у первого
    /// соперника до 28% у грозной Фортуны, внутри главы — от первого соперника к боссу.
    ///
    /// Рычаги сложности только честные и видимые до партии: тактика ИИ (expert уже у потолка силы), сильные особые
    /// кости соперника (lucky, sharper), правило босса, подыгрывающее его костям (действует на обоих), «соперник ходит
    /// первым» (OpponentStartsModifier). Цели: обычные 3000–8000, боссы и грозные — до 10000 (длина партии на телефоне).
    ///
    /// Главы 5–10 переиспользуют локации: сад, чердак, подвал — как есть; таверна ночью, пляж на закате и дом в полночь —
    /// скрытые в магазине варианты локаций (та же модель, свой свет: PrefabPayload.Lighting).
    /// Уже созданные ассеты не перезаписываются. Перенастройка глав 1–4 — таблица v4, применяется один раз
    /// (GameConfig.CampaignBalanceVersion) и только к значениям, которые стоят от генератора (ручные правки остаются).
    /// </summary>
    public static partial class ZonkSetup
    {
        private const int CampaignBalanceV4 = 4;

        private sealed class CampaignOpp
        {
            public string Id;
            public string Ai;
            public string Reactions;
            public Color Color;
            public string Hat;
            public int Target;
            public string[] Dice;
            public MatchModifier[] Rules;
            public StarCondition[] Stars;
            public int FirstCoins;
            public int RepeatCoins;
            public int Energy;
            public string Roll;
        }

        private sealed class CampaignChapter
        {
            public string Asset;
            public string Id;
            public int Order;
            public string Environment;
            public string Speaker;
            public CampaignOpp[] Opponents;
            public CampaignOpp Dread;
        }

        private static string[] L(params string[] dice) => dice;
        private static string[] Six(string die) => Enumerable.Repeat(die, 6).ToArray();
        private static MatchModifier[] R(params MatchModifier[] rules) => rules;
        private static StarCondition[] St(params StarCondition[] stars) => stars;

        private static CampaignOpp Opp(string id, string ai, string reactions, Color color, string hat, int target, string[] dice,
            StarCondition[] stars, int firstCoins, int repeatCoins, string roll = null)
        {
            return new CampaignOpp
            {
                Id = id, Ai = ai, Reactions = reactions, Color = color, Hat = hat, Target = target, Dice = dice,
                Rules = new MatchModifier[0], Stars = stars, FirstCoins = firstCoins, RepeatCoins = repeatCoins, Roll = roll,
            };
        }

        private static CampaignOpp Boss(string id, string ai, Color color, string hat, int target, string[] dice, MatchModifier[] rules,
            StarCondition[] stars, int firstCoins, int repeatCoins, int energy, string roll)
        {
            return new CampaignOpp
            {
                Id = id, Ai = ai, Reactions = id, Color = color, Hat = hat, Target = target, Dice = dice, Rules = rules,
                Stars = stars, FirstCoins = firstCoins, RepeatCoins = repeatCoins, Energy = energy, Roll = roll,
            };
        }

        /// <summary>Главы 5–10. Порядок соперников — по возрастанию силы, последний — босс; Dread — грозная версия.</summary>
        private static CampaignChapter[] ChaptersV4()
        {
            return new[]
            {
                new CampaignChapter
                {
                    Asset = "Ch5_Garden", Id = "garden", Order = 5, Environment = "env_garden", Speaker = "rose",
                    Opponents = new[]
                    {
                        Opp("foma", "cautious", "friendly", new Color(0.35f, 0.5f, 0.25f), "top", 5000, L("lucky", "bone"),
                            St(new MaxTurnsStar { Turns = 9 }, new HotDiceStar { Count = 1 }), 240, 30, "Calm"),
                        Opp("miron", "cautious", "grumpy", new Color(0.75f, 0.6f, 0.2f), "hood", 5500, L("lucky", "sharper"),
                            St(new MaxZonksStar { Zonks = 2 }, new WinByMarginStar { Margin = 1400 }), 250, 30),
                        Opp("lili", "greedy", "friendly", new Color(0.85f, 0.55f, 0.7f), null, 6000, L("lucky", "lucky", "sharper", "sharper"),
                            St(new BigTurnStar { Points = 1500 }, new MaxTurnsStar { Turns = 10 }), 260, 32, "Showman"),
                        Opp("alfred", "balanced", "grumpy", new Color(0.15f, 0.15f, 0.18f), "top", 6000, L("lucky", "sharper", "middle"),
                            St(new NoSpecialDiceStar(), new MaxZonksStar { Zonks = 2 }), 270, 34, "Calm"),
                        Boss("rose", "expert", new Color(0.7f, 0.15f, 0.3f), "top", 8000, L("lucky", "lucky", "lucky", "sharper"),
                            R(new SingleFaceModifier { Face = 1, Multiplier = 2f }),
                            St(new WinByMarginStar { Margin = 2000 }, new MaxZonksStar { Zonks = 2 }), 650, 60, 3, "Showman"),
                    },
                    Dread = Boss("rose_dread", "expert", new Color(0.45f, 0.05f, 0.2f), "top", 9000, Six("lucky"),
                        R(new SingleFaceModifier { Face = 1, Multiplier = 2f }),
                        St(new MaxTurnsStar { Turns = 15 }, new WinByMarginStar { Margin = 2500 }), 800, 90, 5, "Showman"),
                },
                new CampaignChapter
                {
                    Asset = "Ch6_Attic", Id = "attic", Order = 6, Environment = "env_attic", Speaker = "tikhon",
                    Opponents = new[]
                    {
                        Opp("barsik", "greedy", "friendly", new Color(0.95f, 0.6f, 0.25f), null, 5500, L("lucky", "sharper", "odd"),
                            St(new HotDiceStar { Count = 1 }, new MaxTurnsStar { Turns = 9 }), 280, 36, "Quick"),
                        Opp("agrippina", "greedy", "grumpy", new Color(0.75f, 0.8f, 0.9f), "hood", 6000, L("lucky", "lucky", "sharper", "sharper"),
                            St(new MaxZonksStar { Zonks = 2 }, new WinByMarginStar { Margin = 1500 }), 290, 36, "Wild"),
                        Opp("karl", "balanced", "friendly", new Color(0.5f, 0.42f, 0.3f), "top", 5500, L("lucky", "lucky", "even"),
                            St(new MaxTurnsStar { Turns = 9 }, new NoSpecialDiceStar()), 300, 38, "Calm"),
                        Opp("serafim", "balanced", "grumpy", new Color(0.3f, 0.2f, 0.35f), "top", 6500, L("lucky", "lucky", "sharper"),
                            St(new BigTurnStar { Points = 1500 }, new MaxZonksStar { Zonks = 2 }), 310, 40, "Showman"),
                        Boss("tikhon", "expert", new Color(0.45f, 0.4f, 0.3f), "hood", 8000, L("lucky", "lucky", "lucky", "sharper", "sharper"),
                            R(new EntryScoreModifier { EntryScore = 500 }),
                            St(new MaxTurnsStar { Turns = 14 }, new WinByMarginStar { Margin = 2000 }), 700, 65, 3, "Granny"),
                    },
                    Dread = Boss("tikhon_dread", "expert", new Color(0.3f, 0.25f, 0.15f), "hood", 9000,
                        L("lucky", "lucky", "lucky", "lucky", "sharper", "sharper"),
                        R(new EntryScoreModifier { EntryScore = 500 }, new ZonkPenaltyModifier { Penalty = 200 }),
                        St(new MaxZonksStar { Zonks = 3 }, new WinByMarginStar { Margin = 2500 }), 850, 95, 5, "Granny"),
                },
                new CampaignChapter
                {
                    Asset = "Ch7_Basement", Id = "basement", Order = 7, Environment = "env_basement", Speaker = "krot",
                    Opponents = new[]
                    {
                        Opp("twins", "greedy", "friendly", new Color(0.6f, 0.3f, 0.2f), "top", 6000, L("lucky", "lucky", "sharper", "sharper"),
                            St(new MaxTurnsStar { Turns = 10 }, new HotDiceStar { Count = 1 }), 320, 40, "Wild"),
                        Opp("glyba", "balanced", "grumpy", new Color(0.2f, 0.2f, 0.22f), null, 5500, L("lucky", "sharper", "edges"),
                            St(new MaxZonksStar { Zonks = 2 }, new BigTurnStar { Points = 1500 }), 330, 42),
                        Opp("voldemar", "balanced", "friendly", new Color(0.4f, 0.15f, 0.5f), "top", 6000, L("lucky", "lucky", "sharper"),
                            St(new NoSpecialDiceStar(), new WinByMarginStar { Margin = 1500 }), 340, 42, "Showman"),
                        Opp("nina", "expert", "grumpy", new Color(0.45f, 0.5f, 0.55f), "hood", 6500, L("lucky", "lucky", "sharper"),
                            St(new MaxTurnsStar { Turns = 11 }, new MaxZonksStar { Zonks = 2 }), 350, 44, "Calm"),
                        Boss("krot", "expert", new Color(0.3f, 0.22f, 0.18f), "hood", 8000, L("sharper", "sharper", "sharper", "lucky"),
                            R(new OpponentStartsModifier()),
                            St(new MaxZonksStar { Zonks = 2 }, new WinByMarginStar { Margin = 2000 }), 750, 70, 3, "Quick"),
                    },
                    Dread = Boss("krot_dread", "expert", new Color(0.18f, 0.12f, 0.1f), "hood", 9000, Six("sharper"),
                        R(new OpponentStartsModifier(), new MinBankModifier { MinBankScore = 500 }),
                        St(new MaxTurnsStar { Turns = 15 }, new BigTurnStar { Points = 2500 }), 900, 100, 5, "Quick"),
                },
                new CampaignChapter
                {
                    Asset = "Ch8_TavernNight", Id = "tavern_night", Order = 8, Environment = "env_tavern_night", Speaker = "madame",
                    Opponents = new[]
                    {
                        Opp("erofey", "balanced", "grumpy", new Color(0.25f, 0.3f, 0.4f), "hood", 5500, L("lucky", "lucky", "worn"),
                            St(new MaxZonksStar { Zonks = 2 }, new MaxTurnsStar { Turns = 9 }), 360, 46, "Calm"),
                        Opp("aglaya", "balanced", "friendly", new Color(0.5f, 0.25f, 0.55f), "hood", 6000, L("lucky", "lucky", "sharper"),
                            St(new HotDiceStar { Count = 1 }, new WinByMarginStar { Margin = 1500 }), 370, 46, "Showman"),
                        Opp("gordeev", "expert", "grumpy", new Color(0.2f, 0.25f, 0.2f), "pirate", 6000, L("lucky", "lucky", "sharper"),
                            St(new BigTurnStar { Points = 2000 }, new MaxZonksStar { Zonks = 2 }), 380, 48, "Pirate"),
                        Opp("stavkin", "expert", "friendly", new Color(0.6f, 0.5f, 0.2f), "top", 7000, L("lucky", "lucky", "sharper", "sharper"),
                            St(new MaxTurnsStar { Turns = 12 }, new NoSpecialDiceStar()), 390, 50, "Royal"),
                        Boss("madame", "expert", new Color(0.12f, 0.1f, 0.2f), "hood", 9000, Six("lucky"),
                            R(new ZonkPenaltyModifier { Penalty = 200 }),
                            St(new MaxZonksStar { Zonks = 2 }, new WinByMarginStar { Margin = 2200 }), 800, 75, 3, "Royal"),
                    },
                    Dread = Boss("madame_dread", "expert", new Color(0.05f, 0.04f, 0.1f), "hood", 9000, Six("lucky"),
                        R(new ZonkPenaltyModifier { Penalty = 300 }, new OpponentStartsModifier()),
                        St(new MaxZonksStar { Zonks = 3 }, new MaxTurnsStar { Turns = 16 }), 950, 105, 5, "Royal"),
                },
                new CampaignChapter
                {
                    Asset = "Ch9_BeachSunset", Id = "beach_sunset", Order = 9, Environment = "env_beach_sunset", Speaker = "sych",
                    Opponents = new[]
                    {
                        Opp("vetrov", "balanced", "friendly", new Color(0.3f, 0.45f, 0.6f), "pirate", 6000, L("lucky", "sharper", "fives"),
                            St(new MaxTurnsStar { Turns = 10 }, new HotDiceStar { Count = 1 }), 400, 50, "Pirate"),
                        Opp("zhemchugov", "expert", "friendly", new Color(0.85f, 0.85f, 0.9f), "top", 6000, L("lucky", "sharper"),
                            St(new WinByMarginStar { Margin = 1500 }, new MaxZonksStar { Zonks = 2 }), 410, 52, "Calm"),
                        Opp("marta", "expert", "grumpy", new Color(0.55f, 0.15f, 0.12f), "pirate", 6500, L("lucky", "lucky", "sharper", "sharper"),
                            St(new MaxZonksStar { Zonks = 1 }, new BigTurnStar { Points = 2000 }), 420, 54, "Wild"),
                        Opp("strogiy", "expert", "grumpy", new Color(0.2f, 0.28f, 0.45f), "top", 7000,
                            L("lucky", "lucky", "lucky", "sharper", "sharper"),
                            St(new NoSpecialDiceStar(), new MaxTurnsStar { Turns = 12 }), 430, 56, "Quick"),
                        Boss("sych", "expert", new Color(0.35f, 0.3f, 0.25f), "pirate", 9000, Six("sharper"),
                            R(new MinBankModifier { MinBankScore = 500 }),
                            St(new MaxTurnsStar { Turns = 15 }, new WinByMarginStar { Margin = 2200 }), 850, 80, 3, "Pirate"),
                    },
                    Dread = Boss("sych_dread", "expert", new Color(0.2f, 0.17f, 0.12f), "pirate", 9000, Six("sharper"),
                        R(new MinBankModifier { MinBankScore = 500 }, new OpponentStartsModifier(), new ZonkPenaltyModifier { Penalty = 200 }),
                        St(new MaxZonksStar { Zonks = 3 }, new BigTurnStar { Points = 3000 }), 1000, 110, 5, "Pirate"),
                },
                new CampaignChapter
                {
                    Asset = "Ch10_HomeMidnight", Id = "home_midnight", Order = 10, Environment = "env_home_midnight", Speaker = "fortuna",
                    Opponents = new[]
                    {
                        Opp("jester", "expert", "friendly", new Color(0.8f, 0.2f, 0.25f), "top", 6000, L("lucky", "lucky", "sharper"),
                            St(new HotDiceStar { Count = 1 }, new MaxZonksStar { Zonks = 2 }), 450, 58, "Showman"),
                        Opp("karr", "expert", "grumpy", new Color(0.08f, 0.08f, 0.1f), null, 6500, L("lucky", "lucky", "sharper", "sharper"),
                            St(new MaxTurnsStar { Turns = 11 }, new WinByMarginStar { Margin = 1600 }), 460, 60, "Quick"),
                        Opp("sudba", "expert", "friendly", new Color(0.55f, 0.45f, 0.7f), "hood", 7000,
                            L("lucky", "lucky", "lucky", "sharper", "sharper"),
                            St(new NoSpecialDiceStar(), new MaxZonksStar { Zonks = 2 }), 470, 62, "Granny"),
                        Opp("arkhip", "expert", "friendly", new Color(0.55f, 0.55f, 0.6f), "top", 8000,
                            L("lucky", "lucky", "lucky", "sharper", "sharper"),
                            St(new BigTurnStar { Points = 2000 }, new MaxTurnsStar { Turns = 14 }), 500, 64, "Calm"),
                        Boss("fortuna", "expert", new Color(0.9f, 0.75f, 0.3f), "hood", 8000, Six("lucky"),
                            R(new OpponentStartsModifier()),
                            St(new MaxZonksStar { Zonks = 2 }, new WinByMarginStar { Margin = 2000 }), 1000, 90, 5, "Royal"),
                    },
                    Dread = Boss("fortuna_dread", "expert", new Color(0.95f, 0.85f, 0.45f), "hood", 9000, Six("lucky"),
                        R(new SingleFaceModifier { Face = 5, Multiplier = 0f }),
                        St(new MaxZonksStar { Zonks = 3 }, new MaxTurnsStar { Turns = 17 }), 1500, 120, 10, "Royal"),
                },
            };
        }

        /// <summary>Главы 5–10: варианты локаций со своим светом, соперники, боссы, грозные версии, сюжет.</summary>
        private static void BuildCampaignChapters(ArtSet art)
        {
            BuildChapterLocations(art);

            var coins = AssetDatabase.LoadAssetAtPath<CurrencyConfig>(ConfigsFolder + "/Currencies/Coins.asset");
            var energy = AssetDatabase.LoadAssetAtPath<CurrencyConfig>(ConfigsFolder + "/Currencies/Energy.asset");
            foreach (var spec in ChaptersV4())
            {
                var opponents = new List<OpponentConfig>();
                OpponentConfig boss = null;
                foreach (var o in spec.Opponents)
                {
                    var isBoss = o.Energy > 0;
                    if (isBoss)
                        BossReactions(o.Id);
                    var opponent = CampaignOpponent(o, art, coins, energy, isBoss, null);
                    opponents.Add(opponent);
                    if (isBoss)
                        boss = opponent;
                }

                var dread = spec.Dread != null && boss != null ? CampaignOpponent(spec.Dread, art, coins, energy, true, boss) : null;
                var chapter = Asset<ChapterConfig>(ConfigsFolder + "/Chapters/" + spec.Asset + ".asset", c =>
                {
                    Identity(c, "chapter_" + spec.Id, "chapter." + spec.Id);
                    c.Order = spec.Order;
                    c.Environment = ItemAt(spec.Environment);
                    c.Opponents = opponents;
                    c.Intro = new List<StoryLine>
                    {
                        Line("story.narrator", "story." + spec.Id + ".1"),
                        Line("story." + spec.Speaker, "story." + spec.Id + ".2"),
                    };
                    c.Outro = new List<StoryLine> { Line("story." + spec.Speaker, "story." + spec.Id + ".outro") };
                });

                if (dread != null && !chapter.DreadBosses.Contains(dread))
                {
                    chapter.DreadBosses.Add(dread);
                    EditorUtility.SetDirty(chapter);
                }
            }
        }

        private static OpponentConfig CampaignOpponent(CampaignOpp spec, ArtSet art, CurrencyConfig coins, CurrencyConfig energy,
            bool boss, OpponentConfig dreadOf)
        {
            return Asset<OpponentConfig>(ConfigsFolder + "/Opponents/Opp_" + spec.Id + ".asset", o =>
            {
                Identity(o, "opp_" + spec.Id, "opp." + spec.Id);
                o.TitleKey = "opp." + spec.Id + ".title";
                o.Ai = AiAt(spec.Ai);
                o.Reactions = dreadOf != null ? dreadOf.Reactions
                    : AssetDatabase.LoadAssetAtPath<ReactionSetConfig>(ConfigsFolder + "/Reactions/Reactions_" + spec.Reactions + ".asset");
                o.BodyColor = spec.Color;
                o.Accessory = spec.Hat == "hood" ? art.Hood : spec.Hat == "top" ? art.HatTop : spec.Hat == "pirate" ? art.HatPirate : null;
                o.IsBoss = boss;
                o.DreadOf = dreadOf;
                o.DreadUnlockStars = 3;
                o.TargetScore = spec.Target;
                if (spec.Roll != null)
                    o.RollStyle = AssetDatabase.LoadAssetAtPath<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_" + spec.Roll + ".asset");

                o.Dice = new List<DieConfig>();
                foreach (var dieId in spec.Dice)
                {
                    var die = DieAt(dieId);
                    if (die != null)
                        o.Dice.Add(die);
                }

                o.Modifiers.AddRange(spec.Rules);
                o.StarConditions = new List<StarCondition>(spec.Stars);
                if (coins != null)
                {
                    o.FirstWinRewards.Add(Gift(coins, spec.FirstCoins));
                    o.RepeatWinRewards.Add(Gift(coins, spec.RepeatCoins));
                }

                // TODO: косметика за боссов глав 5–10 (столы, стаканы, скины) — когда будут модели из Blender.
                if (energy != null && spec.Energy > 0)
                    o.FirstWinRewards.Add(Gift(energy, spec.Energy));
            });
        }

        /// <summary>Реплики босса: свой набор, ключи line.&lt;id&gt;.* в Texts.csv.</summary>
        private static void BossReactions(string id)
        {
            var key = "line." + id + ".";
            Reactions(id, new[]
            {
                Reaction(MatchEventType.MatchStarted, AvatarGesture.Nod, 1f, key + "start"),
                Reaction(MatchEventType.SelfZonk, AvatarGesture.SlamTable, 0.9f, key + "zonk"),
                Reaction(MatchEventType.OtherZonk, AvatarGesture.Laugh, 0.8f, key + "otherZonk"),
                Reaction(MatchEventType.OtherBigKeep, AvatarGesture.SlamTwice, 0.8f, key + "otherBig"),
                Reaction(MatchEventType.OtherHotDice, AvatarGesture.SlamTwice, 1f, key + "otherBig"),
                Reaction(MatchEventType.Won, AvatarGesture.Laugh, 1f, key + "won"),
                Reaction(MatchEventType.Lost, AvatarGesture.Angry, 1f, key + "lost"),
                Reaction(MatchEventType.Thinking, AvatarGesture.Think, 0.5f),
            });
        }

        /// <summary>
        /// Варианты локаций для глав: та же модель, свой свет. Скрыты в магазине (HiddenInShop) и бесплатны: их не
        /// надевают, они только фон главы (ChapterConfig.Environment ставится на время партии).
        /// </summary>
        private static void BuildChapterLocations(ArtSet art)
        {
            var envSlot = SlotAt("environment");
            if (envSlot == null)
                return;

            var tavernNight = Profile("Lighting_TavernNight", p =>
            {
                p.SunColor = new Color(0.45f, 0.5f, 0.8f);
                p.SunIntensity = 0.12f;
                p.SunRotation = new Vector3(25f, 150f, 0f);
                p.AmbientSky = new Color(0.1f, 0.08f, 0.1f);
                p.AmbientEquator = new Color(0.12f, 0.08f, 0.06f);
                p.AmbientGround = new Color(0.05f, 0.03f, 0.02f);
                p.Fog = true;
                p.FogColor = new Color(0.06f, 0.04f, 0.04f);
                p.FogDensity = 0.04f;
                p.Background = new Color(0.03f, 0.02f, 0.03f);
                p.LampIntensity = 1.8f;
            });

            var beachSunset = Profile("Lighting_BeachSunset", p =>
            {
                p.SunColor = new Color(1f, 0.55f, 0.3f);
                p.SunIntensity = 0.9f;
                p.SunRotation = new Vector3(10f, -70f, 0f);
                p.AmbientSky = new Color(0.55f, 0.4f, 0.45f);
                p.AmbientEquator = new Color(0.5f, 0.35f, 0.3f);
                p.AmbientGround = new Color(0.3f, 0.2f, 0.15f);
                p.Fog = true;
                p.FogColor = new Color(0.95f, 0.6f, 0.45f);
                p.FogDensity = 0.008f;
                p.Background = new Color(0.95f, 0.55f, 0.4f);
                p.LampIntensity = 0.6f;
            });

            var homeMidnight = Profile("Lighting_HomeMidnight", p =>
            {
                p.SunColor = new Color(0.5f, 0.6f, 0.95f);
                p.SunIntensity = 0.2f;
                p.SunRotation = new Vector3(40f, -35f, 0f);
                p.AmbientSky = new Color(0.1f, 0.11f, 0.18f);
                p.AmbientEquator = new Color(0.07f, 0.07f, 0.1f);
                p.AmbientGround = new Color(0.03f, 0.03f, 0.04f);
                p.Fog = true;
                p.FogColor = new Color(0.05f, 0.05f, 0.08f);
                p.FogDensity = 0.03f;
                p.Background = new Color(0.03f, 0.04f, 0.07f);
                p.LampIntensity = 1.6f;
                p.Effects = new List<AtmosphereEffect> { new RainEffect { Prefab = RainPrefab() }, new LightningEffect() };
            });

            ChapterLocation("env_tavern_night", envSlot, 20, art.EnvTavern, tavernNight);
            ChapterLocation("env_beach_sunset", envSlot, 21, art.EnvBeach, beachSunset);
            ChapterLocation("env_home_midnight", envSlot, 22, art.EnvHome, homeMidnight);
        }

        private static void ChapterLocation(string id, CosmeticSlotConfig slot, int order, GameObject prefab, LightingProfileConfig lighting)
        {
            var item = Item(id, slot, order, new PrefabPayload { Prefab = prefab, Lighting = lighting });
            if (!item.HiddenInShop)
            {
                item.HiddenInShop = true;
                EditorUtility.SetDirty(item);
            }

            SetLighting(item, lighting);
        }

        // ---------- Перенастройка глав 1–4 (v4) ----------

        private sealed class OpponentRetune
        {
            public string Id;
            public string AiFrom;
            public string AiTo;
            public int TargetFrom;
            public int TargetTo;
            public string[] DiceFrom;
            public string[] DiceTo;
            public bool OpponentStarts;
        }

        private static OpponentRetune Re(string id, string aiFrom, string aiTo, int targetFrom, int targetTo,
            string[] diceFrom = null, string[] diceTo = null, bool opponentStarts = false)
        {
            return new OpponentRetune
            {
                Id = id, AiFrom = aiFrom, AiTo = aiTo, TargetFrom = targetFrom, TargetTo = targetTo, DiceFrom = diceFrom, DiceTo = diceTo,
                OpponentStarts = opponentStarts,
            };
        }

        /// <summary>
        /// Главы 1–4 под кривую: было ~55% побед почти везде, «хаотичные» соперники в главах 2–3 отдавали 72–75%,
        /// грозные боссы почти не отличались от обычных. Прежнее значение генератора → новое.
        /// </summary>
        private static readonly OpponentRetune[] OpponentsV4 =
        {
            Re("klava", "novice", "chaotic", 5000, 3000),
            Re("petrovich", "greedy", "novice", 4500, 4500, new string[0], L("lucky")),
            Re("semenych", "cautious", "novice", 6000, 5000),
            Re("agafya", null, null, 10000, 6000),
            Re("agafya_dread", null, null, 12000, 8000),
            Re("lutik", null, null, 5500, 3000),
            Re("gustav", "balanced", "novice", 7000, 5500),
            Re("zhora", "expert", "greedy", 6500, 6000),
            Re("bo", "expert", "balanced", 10000, 8000),
            Re("bo_dread", null, null, 12000, 9000, null, null, true),
            Re("stepan", "balanced", "novice", 6000, 6000),
            Re("zina", null, null, 8000, 5500, L("lucky", "worn"), new string[0]),
            Re("max", "chaotic", "greedy", 5500, 5500, L("edges", "odd"), L("edges", "odd", "lucky")),
            Re("efim", null, null, 7000, 7000, L("edges", "worn", "odd"), L("lucky", "lucky", "sharper", "sharper")),
            Re("claw", "expert", "balanced", 10000, 8000),
            Re("claw_dread", null, null, 12000, 8000),
            Re("greta", "expert", "greedy", 8000, 7000, L("sharper", "lucky", "edges"), L("lucky", "lucky", "sharper", "sharper", "edges")),
            Re("hook", null, null, 7000, 5000),
            Re("captain_dread", null, null, 12000, 10000, L("sharper", "lucky", "edges", "odd", "worn", "sixes"),
                L("lucky", "lucky", "sharper", "sharper", "edges", "odd"), true),
        };

        /// <summary>Звёзды под новые цели: прежнее значение → новое.</summary>
        private static readonly (string id, StarKind kind, int from, int to)[] StarsV4 =
        {
            ("opp_klava", StarKind.MaxTurns, 10, 7),
            ("opp_semenych", StarKind.MaxTurns, 12, 10),
            ("opp_agafya", StarKind.MaxZonks, 3, 2), ("opp_agafya", StarKind.Margin, 2400, 1500),
            ("opp_agafya_dread", StarKind.Margin, 3000, 2000),
            ("opp_lutik", StarKind.MaxTurns, 10, 7),
            ("opp_zina", StarKind.Margin, 2100, 1400),
            ("opp_claw_dread", StarKind.Margin, 3500, 2300),
            ("opp_greta", StarKind.MaxTurns, 13, 12),
            ("opp_captain_dread", StarKind.MaxTurns, 18, 16), ("opp_captain_dread", StarKind.Margin, 4000, 3300),
        };

        /// <summary>Перенастройка v4: один раз, после RetuneTargets (v3), только значения от генератора.</summary>
        private static void RetuneCampaignV4(GameConfig config)
        {
            if (config == null || config.CampaignBalanceVersion >= CampaignBalanceV4)
                return;

            var changed = 0;
            foreach (var re in OpponentsV4)
            {
                var opponent = OpponentAt(re.Id);
                if (opponent == null)
                    continue;

                if (re.AiFrom != null && opponent.Ai == AiAt(re.AiFrom))
                {
                    opponent.Ai = AiAt(re.AiTo);
                    changed += Dirty(opponent);
                }

                if (opponent.TargetScore == re.TargetFrom && re.TargetFrom != re.TargetTo)
                {
                    opponent.TargetScore = re.TargetTo;
                    changed += Dirty(opponent);
                }

                if (re.DiceFrom != null && opponent.Dice.Select(d => d != null ? d.Id : null)
                        .SequenceEqual(re.DiceFrom.Select(id => "die_" + id)))
                {
                    opponent.Dice = re.DiceTo.Select(DieAt).Where(d => d != null).ToList();
                    changed += Dirty(opponent);
                }

                if (re.OpponentStarts && !opponent.Modifiers.Any(m => m is OpponentStartsModifier))
                {
                    opponent.Modifiers.Add(new OpponentStartsModifier());
                    changed += Dirty(opponent);
                }
            }

            var byId = ContentById<OpponentConfig>();
            foreach (var (id, kind, from, to) in StarsV4)
            {
                if (!byId.TryGetValue(id, out var opponent))
                    continue;

                foreach (var star in opponent.StarConditions)
                {
                    if (RetuneStar(star, kind, from, to))
                    {
                        changed += Dirty(opponent);
                        break;
                    }
                }
            }

            // У обычных соперников теперь до 5 особых костей (поздние главы — сильные наборы).
            if (config.OpponentMaxSpecialDice == 3)
                config.OpponentMaxSpecialDice = 5;

            // Конец главы 4 больше не «продолжение выйдет с обновлениями»: дальше сад графини Розы.
            var ship = AssetDatabase.LoadAssetAtPath<ChapterConfig>(ConfigsFolder + "/Chapters/Ch4_Ship.asset");
            if (ship != null)
            {
                foreach (var line in ship.Outro)
                {
                    if (line != null && line.TextKey == "story.ship.outro")
                    {
                        line.TextKey = "story.ship.outro2";
                        changed += Dirty(ship);
                    }
                }
            }

            config.CampaignBalanceVersion = CampaignBalanceV4;
            EditorUtility.SetDirty(config);
            Debug.Log($"[Setup] Campaign balance v4 applied ({changed} changes)");
        }
    }
}
