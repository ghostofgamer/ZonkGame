using System.Collections.Generic;
using Base.Editor.Texts;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>Что нужно сценам и ProjectContext из созданного контента.</summary>
    public sealed class ContentSet
    {
        public ContentDatabase Database;
        public GameConfig Config;
        public TextAsset Texts;
        public Zonk.UI.UiConfig Ui;
        public GameObject LoadingScreen;
        public readonly List<CosmeticSlotConfig> Slots = new List<CosmeticSlotConfig>();
    }

    public static partial class ZonkSetup
    {
        public static ContentSet BuildContent(ArtSet art)
        {
            var set = new ContentSet();
            set.Ui = BuildUi(out set.LoadingScreen);

            set.Texts = AssetDatabase.LoadAssetAtPath<TextAsset>(Localization + "/Texts.csv");
            Asset<LocalizationSheet>(Localization + "/TextsSheet.asset", sheet => sheet.Target = set.Texts);

            // Валюты.
            var coins = Asset<CurrencyConfig>(ConfigsFolder + "/Currencies/Coins.asset", c =>
            {
                Identity(c, "coins", "currency.coins");
                c.StartAmount = 300;
                c.Color = new Color(1f, 0.82f, 0.35f);
            });
            var energy = Asset<CurrencyConfig>(ConfigsFolder + "/Currencies/Energy.asset", c =>
            {
                Identity(c, "energy", "currency.energy");
                c.StartAmount = 5;
                c.RegenCap = 5;
                c.RegenAmount = 1;
                c.RegenIntervalSeconds = 480;
                c.Color = new Color(0.45f, 0.85f, 0.45f);
            });

            var classic = Asset<RuleSetConfig>(ConfigsFolder + "/Rules/Classic.asset", r =>
            {
                Identity(r, "rules_classic", "rules.classic");
                r.Rules = RuleSet.CreateClassicRules();
            });

            BuildRules(set.Ui, classic);

            // Кости. Веса подобраны симулятором: 2 особые + 4 обычные против 6 обычных дают 48–52% побед.
            var standard = Die("standard", "die.standard", null, new[] { 1f, 1f, 1f, 1f, 1f, 1f }, Color.clear);
            var lucky = Die("lucky", "die.lucky", "die.lucky.desc", new[] { 1.3f, 1f, 1f, 1f, 0.6f, 1f }, new Color(1f, 0.8f, 0.2f));
            var even = Die("even", "die.even", "die.even.desc", new[] { 1.05f, 1.25f, 0.8f, 1.25f, 1.05f, 1.25f }, new Color(0.3f, 0.6f, 1f));
            var odd = Die("odd", "die.odd", "die.odd.desc", new[] { 1.1f, 0.9f, 1.4f, 0.9f, 1.1f, 0.9f }, new Color(0.8f, 0.35f, 1f));
            var sixes = Die("sixes", "die.sixes", "die.sixes.desc", new[] { 0.9f, 1f, 1f, 1f, 1f, 1.5f }, new Color(1f, 0.35f, 0.3f));
            var fives = Die("fives", "die.fives", "die.fives.desc", new[] { 0.7f, 1f, 1f, 1f, 1.5f, 1f }, new Color(0.3f, 0.9f, 0.5f));

            // Слоты косметики: ID слота = ID якоря в сцене, ракурс камеры = ID CameraShot.
            var envSlot = Slot("environment", "slot.environment", 0, "shop_environment", new AnchorPrefabApplier());
            var tableSlot = Slot("table", "slot.table", 1, "shop_table", new AnchorPrefabApplier());
            var traySlot = Slot("tray", "slot.tray", 2, "shop_tray", new AnchorPrefabApplier());
            var cupSlot = Slot("cup", "slot.cup", 3, "shop_cup", new AnchorPrefabApplier());
            var skinSlot = Slot("dice_skin", "slot.dice_skin", 4, "shop_dice", new DiceSkinApplier());
            var lampSlot = Slot("lamp", "slot.lamp", 5, "shop_lamp", new AnchorPrefabApplier());
            set.Slots.AddRange(new[] { envSlot, tableSlot, traySlot, cupSlot, skinSlot, lampSlot });

            var envHome = Item("env_home", envSlot, 0, new PrefabPayload { Prefab = art.EnvHome });
            var envTavern = Item("env_tavern", envSlot, 1, new PrefabPayload { Prefab = art.EnvTavern },
                new ProgressPriceOption { HintKey = "hint.tavern" }, Coins(coins, 1500));
            var envBeach = Item("env_beach", envSlot, 2, new PrefabPayload { Prefab = art.EnvBeach },
                Coins(coins, 2000), new RewardedAdPriceOption { AdsRequired = 5 });
            var envShip = Item("env_ship", envSlot, 3, new PrefabPayload { Prefab = art.EnvShip }, Coins(coins, 3000));

            var tableOak = Item("table_oak", tableSlot, 0, new PrefabPayload { Prefab = art.Table });
            var tableDark = Item("table_dark", tableSlot, 1, new MaterialPayload { Material = art["Table_Dark"] }, Coins(coins, 400));
            Item("table_marble", tableSlot, 2, new MaterialPayload { Material = art["Table_Marble"] }, Coins(coins, 700));

            var feltGreen = Item("felt_green", traySlot, 0, new PrefabPayload { Prefab = art.Felt });
            var feltRed = Item("felt_red", traySlot, 1, new MaterialPayload { Material = art["Felt_Red"] }, Coins(coins, 250));
            Item("felt_blue", traySlot, 2, new MaterialPayload { Material = art["Felt_Blue"] }, new RewardedAdPriceOption { AdsRequired = 3 });

            var cupLeather = Item("cup_leather", cupSlot, 0, new PrefabPayload { Prefab = art.CupLeather });
            var cupWood = Item("cup_wood", cupSlot, 1, new PrefabPayload { Prefab = art.CupWood }, Coins(coins, 300));
            Item("cup_gold", cupSlot, 2, new MaterialPayload { Material = art["Cup_Gold"] }, Coins(coins, 900),
                new PurchasePriceOption { ProductId = "cup_gold" });

            var skinIvory = Item("skin_ivory", skinSlot, 0, new MeshMaterialPayload { Material = art["Die_Ivory"] });
            var skinSapphire = Item("skin_sapphire", skinSlot, 1, new MeshMaterialPayload { Material = art["Die_Sapphire"] });
            Item("skin_ruby", skinSlot, 2, new MeshMaterialPayload { Material = art["Die_Ruby"] }, Coins(coins, 300));
            Item("skin_jade", skinSlot, 3, new MeshMaterialPayload { Material = art["Die_Jade"] }, new RewardedAdPriceOption { AdsRequired = 3 });
            Item("skin_obsidian", skinSlot, 4, new MeshMaterialPayload { Material = art["Die_Obsidian"] }, Coins(coins, 600));
            Item("skin_gold", skinSlot, 5, new MeshMaterialPayload { Material = art["Die_Gold"] }, Coins(coins, 1200));

            var lampBasic = Item("lamp_basic", lampSlot, 0, new PrefabPayload { Prefab = art.LampBasic });
            Item("lamp_lantern", lampSlot, 1, new PrefabPayload { Prefab = art.LampLantern }, Coins(coins, 350));

            SetDefault(envSlot, envHome);
            SetDefault(tableSlot, tableOak);
            SetDefault(traySlot, feltGreen);
            SetDefault(cupSlot, cupLeather);
            SetDefault(skinSlot, skinIvory);
            SetDefault(lampSlot, lampBasic);

            Asset<ThemeSetConfig>(ConfigsFolder + "/Themes/Pirate.asset", t =>
            {
                Identity(t, "theme_pirate", "theme.pirate");
                t.Items = new List<CosmeticItemConfig> { envShip, tableDark, feltRed, cupWood };
                t.Price.Options.Add(new PurchasePriceOption { ProductId = "theme_pirate" });
                t.Price.Options.Add(Coins(coins, 4000));
            });

            // Характеры ИИ. Победы против «среднего игрока» (симулятор): азартный 34%, новичок 44%, жадный 47%,
            // осторожный 49%, расчётливый 55%, мастер 56%.
            var aiNovice = Ai("novice", new GreedySelection(),
                new ThresholdRisk { BankAt = new[] { 200, 250, 300, 350, 600, 100000 }, BehindAggression = 0f, AheadCaution = 0f, Jitter = 0.3f }, 0.3f);
            var aiCautious = Ai("cautious", new GreedySelection(),
                new ThresholdRisk { BankAt = new[] { 250, 250, 300, 400, 800, 100000 }, BehindAggression = 0.2f, Jitter = 0.15f }, 0.15f);
            var aiChaotic = Ai("chaotic", new GreedySelection(), new RandomRisk { MinThreshold = 200, MaxThreshold = 1600 }, 0.1f);
            var aiGreedy = Ai("greedy", new GreedySelection(),
                new ThresholdRisk { BankAt = new[] { 500, 550, 700, 1100, 2500, 100000 }, BehindAggression = 0.8f, Jitter = 0.15f }, 0.08f);
            var aiBalanced = Ai("balanced", new ValueSelection { DieValue = 60 }, new ThresholdRisk(), 0.06f);
            var aiExpert = Ai("expert", new ValueSelection { DieValue = 60 },
                new ThresholdRisk { BankAt = new[] { 300, 300, 400, 700, 2000, 100000 }, BehindAggression = 0.7f, AheadCaution = 0.4f, Jitter = 0.05f }, 0f);

            var friendly = Reactions("friendly", new[]
            {
                Reaction(MatchEventType.MatchStarted, AvatarGesture.Nod, 1f, "line.friendly.start1", "line.friendly.start2"),
                Reaction(MatchEventType.SelfZonk, AvatarGesture.Shrug, 0.8f, "line.friendly.zonk1", "line.friendly.zonk2"),
                Reaction(MatchEventType.OtherZonk, AvatarGesture.Nod, 0.5f, "line.friendly.otherZonk"),
                Reaction(MatchEventType.SelfBigBank, AvatarGesture.Cheer, 0.8f, "line.friendly.bigBank"),
                Reaction(MatchEventType.OtherBigBank, AvatarGesture.Nod, 0.6f, "line.friendly.otherBigBank"),
                Reaction(MatchEventType.Won, AvatarGesture.Cheer, 1f, "line.friendly.won"),
                Reaction(MatchEventType.Lost, AvatarGesture.Nod, 1f, "line.friendly.lost"),
                Reaction(MatchEventType.Thinking, AvatarGesture.Think, 0.6f, "line.friendly.think"),
                Reaction(MatchEventType.SelfRiskyRoll, AvatarGesture.None, 0.7f, "line.friendly.risky"),
                Reaction(MatchEventType.PhraseReceived, AvatarGesture.Nod, 0.8f, "line.friendly.phrase"),
            });
            var grumpy = Reactions("grumpy", new[]
            {
                Reaction(MatchEventType.MatchStarted, AvatarGesture.None, 1f, "line.grumpy.start"),
                Reaction(MatchEventType.SelfZonk, AvatarGesture.SlamTable, 0.9f, "line.grumpy.zonk"),
                Reaction(MatchEventType.OtherZonk, AvatarGesture.Laugh, 0.7f, "line.grumpy.otherZonk"),
                Reaction(MatchEventType.SelfBigBank, AvatarGesture.Nod, 0.7f, "line.grumpy.bigBank"),
                Reaction(MatchEventType.OtherBigBank, AvatarGesture.Angry, 0.7f, "line.grumpy.otherBigBank"),
                Reaction(MatchEventType.Won, AvatarGesture.Laugh, 1f, "line.grumpy.won"),
                Reaction(MatchEventType.Lost, AvatarGesture.SlamTable, 1f, "line.grumpy.lost"),
                Reaction(MatchEventType.Thinking, AvatarGesture.Think, 0.5f),
                Reaction(MatchEventType.PhraseReceived, AvatarGesture.Angry, 0.8f, "line.grumpy.phrase"),
            });
            var agafyaLines = Reactions("agafya", new[]
            {
                Reaction(MatchEventType.MatchStarted, AvatarGesture.Nod, 1f, "line.agafya.start"),
                Reaction(MatchEventType.SelfZonk, AvatarGesture.Shrug, 0.8f, "line.agafya.zonk"),
                Reaction(MatchEventType.OtherZonk, AvatarGesture.Laugh, 0.7f, "line.agafya.otherZonk"),
                Reaction(MatchEventType.Won, AvatarGesture.Laugh, 1f, "line.agafya.won"),
                Reaction(MatchEventType.Lost, AvatarGesture.Nod, 1f, "line.agafya.lost"),
                Reaction(MatchEventType.Thinking, AvatarGesture.Think, 0.5f),
            });
            var boLines = Reactions("bo", new[]
            {
                Reaction(MatchEventType.MatchStarted, AvatarGesture.SlamTable, 1f, "line.bo.start"),
                Reaction(MatchEventType.SelfZonk, AvatarGesture.SlamTable, 0.9f, "line.bo.zonk"),
                Reaction(MatchEventType.OtherZonk, AvatarGesture.Laugh, 0.8f, "line.bo.otherZonk"),
                Reaction(MatchEventType.SelfBigBank, AvatarGesture.Cheer, 0.8f, "line.bo.bigBank"),
                Reaction(MatchEventType.Won, AvatarGesture.Laugh, 1f, "line.bo.won"),
                Reaction(MatchEventType.Lost, AvatarGesture.Angry, 1f, "line.bo.lost"),
                Reaction(MatchEventType.Thinking, AvatarGesture.Think, 0.5f),
            });

            // Стили броска: обычный, спокойный (осторожные соперники) и резкий (азартные и боссы).
            var rollDefault = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Default.asset", s => { });
            var rollCalm = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Calm.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.5f, 0.7f);
                s.ShakeAmplitude = new Vector2(0.025f, 0.045f);
                s.ShakeFrequency = new Vector2(14f, 18f);
                s.ShakeTilt = new Vector2(5f, 9f);
                s.PourAngle = new Vector2(90f, 105f);
                s.ThrowSpeed = new Vector2(1.9f, 2.6f);
                s.SpinSpeed = new Vector2(6f, 11f);
            });
            var rollWild = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Wild.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.8f, 1.2f);
                s.ShakeAmplitude = new Vector2(0.07f, 0.12f);
                s.ShakeFrequency = new Vector2(24f, 32f);
                s.ShakeTilt = new Vector2(14f, 24f);
                s.PourAngle = new Vector2(110f, 135f);
                s.DirectionJitter = 22f;
                s.ThrowSpeed = new Vector2(3f, 3.8f);
                s.SpinSpeed = new Vector2(14f, 22f);
            });

            // Удары по столу, когда игрок набирает много. Уже созданным наборам добавляются только недостающие события.
            EnsureReaction(friendly, Reaction(MatchEventType.OtherBigKeep, AvatarGesture.SlamTable, 0.5f, "line.friendly.otherBigBank"));
            EnsureReaction(friendly, Reaction(MatchEventType.OtherHotDice, AvatarGesture.Cheer, 0.6f, "line.friendly.otherBigBank"));
            EnsureReaction(grumpy, Reaction(MatchEventType.OtherBigKeep, AvatarGesture.SlamTable, 0.9f, "line.grumpy.otherBig"));
            EnsureReaction(grumpy, Reaction(MatchEventType.OtherHotDice, AvatarGesture.SlamTwice, 0.9f, "line.grumpy.otherBig"));
            EnsureReaction(agafyaLines, Reaction(MatchEventType.OtherBigKeep, AvatarGesture.SlamTable, 0.8f, "line.agafya.otherBig"));
            EnsureReaction(agafyaLines, Reaction(MatchEventType.OtherHotDice, AvatarGesture.SlamTable, 0.9f, "line.agafya.otherBig"));
            EnsureReaction(agafyaLines, Reaction(MatchEventType.OtherBigBank, AvatarGesture.Angry, 0.7f, "line.agafya.otherBig"));
            EnsureReaction(boLines, Reaction(MatchEventType.OtherBigKeep, AvatarGesture.SlamTwice, 0.9f, "line.bo.otherBig"));
            EnsureReaction(boLines, Reaction(MatchEventType.OtherHotDice, AvatarGesture.SlamTwice, 1f, "line.bo.otherBig"));
            EnsureReaction(boLines, Reaction(MatchEventType.OtherBigBank, AvatarGesture.SlamTable, 0.8f, "line.bo.otherBig"));

            // Глава 1: Родной дом. Соперники по возрастанию силы, босс с правилом «три Зонка подряд = −500».
            var vitya = Opponent("vitya", aiChaotic, friendly, new Color(0.3f, 0.35f, 0.5f), null, 60, 20);
            var klava = Opponent("klava", aiNovice, friendly, new Color(0.55f, 0.3f, 0.35f), art.Hood, 70, 20);
            var petrovich = Opponent("petrovich", aiGreedy, grumpy, new Color(0.3f, 0.3f, 0.3f), art.HatTop, 80, 25);
            var semenych = Opponent("semenych", aiCautious, friendly, new Color(0.25f, 0.35f, 0.55f), art.HatTop, 90, 25, fives);
            var agafya = Opponent("agafya", aiBalanced, agafyaLines, new Color(0.4f, 0.3f, 0.45f), art.Hood, 200, 60, lucky,
                boss: true, rule: new ThreeZonkPenaltyModifier { Penalty = 500 });

            // Глава 2: Таверна. Босс: тройки и больше вдвое дороже.
            var lutik = Opponent("lutik", aiChaotic, friendly, new Color(0.3f, 0.5f, 0.35f), art.HatPirate, 100, 30, null, dice: new[] { odd, odd });
            var gustav = Opponent("gustav", aiBalanced, grumpy, new Color(0.45f, 0.35f, 0.25f), null, 110, 30);
            var irma = Opponent("irma", aiGreedy, grumpy, new Color(0.5f, 0.25f, 0.2f), art.Hood, 120, 35, even, dice: new[] { sixes, sixes });
            var zhora = Opponent("zhora", aiExpert, friendly, new Color(0.2f, 0.2f, 0.25f), art.HatTop, 140, 40, odd);
            var bo = Opponent("bo", aiExpert, boLines, new Color(0.35f, 0.2f, 0.2f), art.HatPirate, 300, 80, sixes,
                boss: true, rule: new ComboMultiplierModifier { Category = ComboCategory.OfAKind, Multiplier = 2f }, extraReward: envTavern,
                cup: cupWood);

            Asset<ChapterConfig>(ConfigsFolder + "/Chapters/Ch1_Home.asset", c =>
            {
                Identity(c, "chapter_home", "chapter.home");
                c.Order = 1;
                c.Environment = envHome;
                c.Opponents = new List<OpponentConfig> { vitya, klava, petrovich, semenych, agafya };
                c.Intro = new List<StoryLine>
                {
                    Line("story.narrator", "story.home.1"),
                    Line("story.letter", "story.home.2"),
                    Line("story.narrator", "story.home.3"),
                };
                c.Outro = new List<StoryLine> { Line("story.agafya", "story.home.outro") };
            });

            Asset<ChapterConfig>(ConfigsFolder + "/Chapters/Ch2_Tavern.asset", c =>
            {
                Identity(c, "chapter_tavern", "chapter.tavern");
                c.Order = 2;
                c.Environment = envTavern;
                c.Opponents = new List<OpponentConfig> { lutik, gustav, irma, zhora, bo };
                c.Intro = new List<StoryLine> { Line("story.narrator", "story.tavern.1"), Line("story.bo", "story.tavern.2") };
                c.Outro = new List<StoryLine> { Line("story.bo", "story.tavern.outro") };
            });

            var phraseOrder = 0;
            foreach (var phrase in new[] { "hi", "luck", "wow", "oops", "lucky", "gg" })
            {
                var order = phraseOrder++;
                Asset<PhraseConfig>(ConfigsFolder + "/Phrases/" + phrase + ".asset", p =>
                {
                    Identity(p, "phrase_" + phrase, "phrase." + phrase);
                    p.Order = order;
                });
            }

            var hotSeat = Asset<GameModeConfig>(ConfigsFolder + "/Modes/HotSeat.asset", m =>
            {
                Identity(m, "mode_hotseat", "mode.hotseat");
                m.DescriptionKey = "mode.hotseat.desc";
                m.Rules = classic;
                m.EnergyCost = 0;
            });
            var campaign = Asset<GameModeConfig>(ConfigsFolder + "/Modes/Campaign.asset", m =>
            {
                Identity(m, "mode_campaign", "mode.campaign");
                m.DescriptionKey = "mode.campaign.desc";
                m.Rules = classic;
                m.EnergyCost = 1;
            });

            set.Config = Asset<GameConfig>(ConfigsFolder + "/Game/GameConfig.asset", g =>
            {
                g.HotSeatMode = hotSeat;
                g.CampaignMode = campaign;
                g.Coins = coins;
                g.Energy = energy;
                g.StandardDie = standard;
                g.SecondPlayerFallbackSkin = skinSapphire;
                g.Ui = set.Ui;
            });

            if (set.Config.RollStyle == null)
            {
                set.Config.RollStyle = rollDefault;
                EditorUtility.SetDirty(set.Config);
            }

            // Свой стиль броска у характерных соперников. Уже созданным ассетам — только если не задан руками.
            SetRollStyle(rollCalm, semenych, gustav, klava);
            SetRollStyle(rollWild, petrovich, irma, lutik, vitya, bo);

            // Ещё стили броска: быстрый, показной и фирменные стили боссов.
            var rollQuick = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Quick.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.25f, 0.4f);
                s.ShakeAmplitude = new Vector2(0.04f, 0.06f);
                s.ShakeFrequency = new Vector2(28f, 34f);
                s.PourAngle = new Vector2(100f, 115f);
                s.ThrowSpeed = new Vector2(2.8f, 3.4f);
            });
            var rollShowman = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Showman.asset", s =>
            {
                s.ShakeDuration = new Vector2(1.2f, 1.6f);
                s.ShakeAmplitude = new Vector2(0.09f, 0.14f);
                s.ShakeFrequency = new Vector2(9f, 13f);
                s.ShakeTilt = new Vector2(25f, 35f);
                s.PourAngle = new Vector2(120f, 140f);
                s.SpinSpeed = new Vector2(16f, 24f);
            });
            var rollGranny = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Granny.asset", s =>
            {
                s.ShakeDuration = new Vector2(0.9f, 1.2f);
                s.ShakeAmplitude = new Vector2(0.015f, 0.03f);
                s.ShakeFrequency = new Vector2(8f, 11f);
                s.ShakeTilt = new Vector2(4f, 7f);
                s.PourAngle = new Vector2(85f, 95f);
                s.ThrowSpeed = new Vector2(1.6f, 2.1f);
                s.SpinSpeed = new Vector2(4f, 8f);
            });
            var rollPirate = Asset<RollStyleConfig>(ConfigsFolder + "/Game/RollStyle_Pirate.asset", s =>
            {
                s.ShakeDuration = new Vector2(1f, 1.4f);
                s.ShakeAmplitude = new Vector2(0.1f, 0.15f);
                s.ShakeFrequency = new Vector2(20f, 28f);
                s.ShakeTilt = new Vector2(20f, 30f);
                s.PourAngle = new Vector2(125f, 145f);
                s.DirectionJitter = 25f;
                s.ThrowSpeed = new Vector2(3.2f, 3.9f);
                s.SpinSpeed = new Vector2(18f, 26f);
            });

            // Замах у каждого стиля свой: бабушка почти не замахивается, пират отводит стакан далеко и швыряет.
            TuneThrow(rollCalm, 0.12f, 0.2f, 5f, 10f, 0.25f, 0.35f, 0.02f, 0.05f, 0.2f, 0.26f, 0.05f, 0.1f);
            TuneThrow(rollQuick, 0.2f, 0.3f, 10f, 18f, 0.12f, 0.16f, 0f, 0.02f, 0.1f, 0.13f, 0.1f, 0.18f);
            TuneThrow(rollWild, 0.5f, 0.7f, 25f, 40f, 0.22f, 0.3f, 0.08f, 0.16f, 0.1f, 0.14f, 0.25f, 0.35f);
            TuneThrow(rollShowman, 0.6f, 0.8f, 30f, 45f, 0.35f, 0.45f, 0.15f, 0.3f, 0.14f, 0.18f, 0.2f, 0.3f);
            TuneThrow(rollGranny, 0.05f, 0.1f, 3f, 6f, 0.3f, 0.4f, 0f, 0.03f, 0.3f, 0.4f, 0.02f, 0.05f);
            TuneThrow(rollPirate, 0.6f, 0.8f, 35f, 50f, 0.25f, 0.32f, 0.1f, 0.2f, 0.09f, 0.12f, 0.3f, 0.4f);

            // Слот «Бросок» в магазине: мультивыбор, на каждый бросок случайный стиль из отмеченных.
            var rollSlot = Slot("roll_style", "slot.roll_style", 6, "shop_cup", new NoSceneApplier());
            if (!rollSlot.MultiSelect)
            {
                rollSlot.MultiSelect = true;
                EditorUtility.SetDirty(rollSlot);
            }

            var rollClassic = Item("roll_classic", rollSlot, 0, new RollStylePayload { Style = rollDefault });
            Item("roll_calm", rollSlot, 1, new RollStylePayload { Style = rollCalm }, Coins(coins, 300));
            Item("roll_quick", rollSlot, 2, new RollStylePayload { Style = rollQuick }, Coins(coins, 300));
            Item("roll_wild", rollSlot, 3, new RollStylePayload { Style = rollWild }, Coins(coins, 500));
            Item("roll_showman", rollSlot, 4, new RollStylePayload { Style = rollShowman }, new RewardedAdPriceOption { AdsRequired = 3 });
            var rollGrannyItem = Item("roll_granny", rollSlot, 5, new RollStylePayload { Style = rollGranny },
                new ProgressPriceOption { HintKey = "hint.granny" });
            var rollPirateItem = Item("roll_pirate", rollSlot, 6, new RollStylePayload { Style = rollPirate },
                new ProgressPriceOption { HintKey = "hint.pirate" });
            SetDefault(rollSlot, rollClassic);

            // Фирменный стиль босса задаётся в его конфиге и достаётся игроку за первую победу.
            SetBossRollStyle(agafya, rollGranny, rollGrannyItem, rollDefault, rollWild, rollCalm);
            SetBossRollStyle(bo, rollPirate, rollPirateItem, rollDefault, rollWild, rollCalm);

            if (set.Config.Ui == null)
            {
                set.Config.Ui = set.Ui;
                EditorUtility.SetDirty(set.Config);
            }

            set.Database = Asset<ContentDatabase>(ConfigsFolder + "/Game/ContentDatabase.asset", d => { });
            return set;
        }

        /// <summary>
        /// Стиль босса: ставится, если у босса пусто или стоит один из общих стилей (ручной выбор не трогаем),
        /// и предмет стиля добавляется в награду за первую победу.
        /// </summary>
        private static void SetBossRollStyle(OpponentConfig boss, RollStyleConfig style, CosmeticItemConfig reward,
            params RollStyleConfig[] generic)
        {
            if (boss == null)
                return;

            if (boss.RollStyle == null || System.Array.IndexOf(generic, boss.RollStyle) >= 0)
                boss.RollStyle = style;

            var hasReward = false;
            foreach (var existing in boss.FirstWinRewards)
            {
                if (existing is ContentReward content && content.Item == reward)
                    hasReward = true;
            }

            if (!hasReward)
                boss.FirstWinRewards.Add(new ContentReward { Item = reward });

            EditorUtility.SetDirty(boss);
        }

        /// <summary>
        /// Параметры замаха стиля. Меняются, только если замах ещё стоит по умолчанию: ручные правки не затираются.
        /// </summary>
        private static void TuneThrow(RollStyleConfig style, float distanceMin, float distanceMax, float tiltMin, float tiltMax,
            float durationMin, float durationMax, float holdMin, float holdMax, float swingMin, float swingMax,
            float followMin, float followMax)
        {
            var defaults = ScriptableObject.CreateInstance<RollStyleConfig>();
            var untouched = style.WindUpDistance == defaults.WindUpDistance && style.WindUpTilt == defaults.WindUpTilt &&
                            style.SwingDuration == defaults.SwingDuration;
            Object.DestroyImmediate(defaults);
            if (!untouched)
                return;

            style.WindUpDistance = new Vector2(distanceMin, distanceMax);
            style.WindUpTilt = new Vector2(tiltMin, tiltMax);
            style.WindUpDuration = new Vector2(durationMin, durationMax);
            style.WindUpHold = new Vector2(holdMin, holdMax);
            style.SwingDuration = new Vector2(swingMin, swingMax);
            style.FollowThrough = new Vector2(followMin, followMax);
            EditorUtility.SetDirty(style);
        }

        private static void SetRollStyle(RollStyleConfig style, params OpponentConfig[] opponents)
        {
            foreach (var opponent in opponents)
            {
                if (opponent == null || opponent.RollStyle != null)
                    continue;

                opponent.RollStyle = style;
                EditorUtility.SetDirty(opponent);
            }
        }

        private static void Identity(ContentConfig config, string id, string nameKey)
        {
            config.Id = id;
            config.NameKey = nameKey;
        }

        private static CurrencyPriceOption Coins(CurrencyConfig coins, int amount)
        {
            return new CurrencyPriceOption { Currency = coins, Amount = amount };
        }

        private static DieConfig Die(string id, string nameKey, string descriptionKey, float[] weights, Color marker)
        {
            return Asset<DieConfig>(ConfigsFolder + "/Dice/Die_" + id + ".asset", d =>
            {
                Identity(d, "die_" + id, nameKey);
                d.DescriptionKey = descriptionKey;
                d.Weights = weights;
                d.MarkerColor = marker;

                // Особые кости выдаются соперниками, в магазине не продаются.
                if (marker.a > 0f)
                    d.Price.Options.Add(new ProgressPriceOption());
            });
        }

        private static CosmeticSlotConfig Slot(string id, string nameKey, int order, string shot, CosmeticApplier applier)
        {
            return Asset<CosmeticSlotConfig>(ConfigsFolder + "/Cosmetics/Slots/Slot_" + id + ".asset", s =>
            {
                Identity(s, id, nameKey);
                s.Order = order;
                s.CameraShotId = shot;
                s.Applier = applier;
            });
        }

        private static CosmeticItemConfig Item(string id, CosmeticSlotConfig slot, int order, CosmeticPayload payload,
            params PriceOption[] price)
        {
            return Asset<CosmeticItemConfig>(ConfigsFolder + "/Cosmetics/Items/" + id + ".asset", i =>
            {
                Identity(i, id, "item." + id);
                i.Slot = slot;
                i.Order = order;
                i.Payload = payload;
                i.Price.Options.AddRange(price);
            });
        }

        private static void SetDefault(CosmeticSlotConfig slot, CosmeticItemConfig item)
        {
            if (slot.DefaultItem != null)
                return;

            slot.DefaultItem = item;
            EditorUtility.SetDirty(slot);
        }

        private static AiProfileConfig Ai(string id, AiSelectionPolicy selection, AiRiskPolicy risk, float mistakes)
        {
            return Asset<AiProfileConfig>(ConfigsFolder + "/Ai/Ai_" + id + ".asset", a =>
            {
                Identity(a, "ai_" + id, "ai." + id);
                a.Selection = selection;
                a.Risk = risk;
                a.MistakeChance = mistakes;
            });
        }

        private static ReactionEntry Reaction(MatchEventType type, AvatarGesture gesture, float chance, params string[] lines)
        {
            return new ReactionEntry { Event = type, Gesture = gesture, Chance = chance, LineKeys = new List<string>(lines) };
        }

        /// <summary>Добавить реакцию на событие, если в наборе её ещё нет (ручные правки набора не трогаются).</summary>
        private static void EnsureReaction(ReactionSetConfig set, ReactionEntry entry)
        {
            foreach (var existing in set.Entries)
            {
                if (existing != null && existing.Event == entry.Event)
                    return;
            }

            set.Entries.Add(entry);
            EditorUtility.SetDirty(set);
        }

        private static ReactionSetConfig Reactions(string id, ReactionEntry[] entries)
        {
            return Asset<ReactionSetConfig>(ConfigsFolder + "/Reactions/Reactions_" + id + ".asset", r =>
            {
                Identity(r, "reactions_" + id, "reactions." + id);
                r.Entries = new List<ReactionEntry>(entries);
            });
        }

        private static OpponentConfig Opponent(string id, AiProfileConfig ai, ReactionSetConfig reactions, Color color,
            GameObject accessory, int firstCoins, int repeatCoins, DieConfig rewardDie = null, bool boss = false,
            MatchModifier rule = null, ContentConfig extraReward = null, DieConfig[] dice = null, CosmeticItemConfig cup = null)
        {
            return Asset<OpponentConfig>(ConfigsFolder + "/Opponents/Opp_" + id + ".asset", o =>
            {
                Identity(o, "opp_" + id, "opp." + id);
                o.TitleKey = "opp." + id + ".title";
                o.Ai = ai;
                o.Reactions = reactions;
                o.BodyColor = color;
                o.Accessory = accessory;
                o.IsBoss = boss;
                o.Cup = cup;
                if (dice != null)
                    o.Dice = new List<DieConfig>(dice);
                if (rule != null)
                {
                    o.Modifiers.Add(rule);
                    o.RuleKey = "opp." + id + ".rule";
                }

                var coins = AssetDatabase.LoadAssetAtPath<CurrencyConfig>(ConfigsFolder + "/Currencies/Coins.asset");
                o.FirstWinRewards.Add(new CurrencyReward { Currency = coins, Amount = firstCoins });
                if (rewardDie != null)
                    o.FirstWinRewards.Add(new ContentReward { Item = rewardDie });
                if (extraReward != null)
                    o.FirstWinRewards.Add(new ContentReward { Item = extraReward });
                o.RepeatWinRewards.Add(new CurrencyReward { Currency = coins, Amount = repeatCoins });
            });
        }

        private static StoryLine Line(string speakerKey, string textKey)
        {
            return new StoryLine { SpeakerKey = speakerKey, TextKey = textKey };
        }
    }
}
