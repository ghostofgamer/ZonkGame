using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI;
using Zonk.UI.Transitions;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Префабы окон интерфейса. Вёрстка по правилам AGENTS.md: корень растянут на экран, содержимое в панели
    /// не больше 1760×1000, элементы у краёв привязаны к своим углам, списки — прокрутка, текст — TMP с автоподбором.
    /// Повторяющиеся элементы (вкладки, карточки, строки) — неактивные шаблоны внутри префаба.
    /// Существующие префабы не пересоздаются; пересоздать: Zonk/Setup/Rebuild UI Prefabs.
    /// </summary>
    public static partial class ZonkSetup
    {
        public static readonly string[] WindowPrefabs =
        {
            "LoadingScreen", "RulesWindow", "MainMenuWindow", "SettingsWindow", "ConfirmWindow", "StoryWindow", "ResultsWindow",
            "HotSeatSetupWindow", "CampaignWindow", "ShopWindow", "MatchHudWindow", "QuestsWindow", "LeaderboardWindow", "TutorialTipWindow", "LanguageWindow",
            "ChallengeWindow", "PerkChoiceWindow",
        };

        private static UiConfig _ui;

        public static void BuildWindows(UiConfig ui)
        {
            _ui = ui;
            var popupIn = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Popup_In.asset");
            var popupOut = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Popup_Out.asset");
            var fadeIn = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Fade_In.asset");
            var fadeOut = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Fade_Out.asset");
            var slideLeft = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Slide_In_Left.asset");
            var slideOutLeft = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Slide_Out_Left.asset");
            var slideBottom = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Slide_In_Bottom.asset");
            var slideOutBottom = AssetDatabase.LoadAssetAtPath<UiTransitionConfig>(TransitionsFolder + "/Slide_Out_Bottom.asset");

            WindowPrefab<MainMenuWindow>("MainMenuWindow", slideLeft, slideOutLeft, false, BuildMainMenu);
            WindowPrefab<SettingsWindow>("SettingsWindow", popupIn, popupOut, true, BuildSettings);
            WindowPrefab<ConfirmWindow>("ConfirmWindow", popupIn, popupOut, true, BuildConfirm);
            WindowPrefab<StoryWindow>("StoryWindow", slideBottom, slideOutBottom, false, BuildStory);
            WindowPrefab<ResultsWindow>("ResultsWindow", popupIn, popupOut, true, BuildResults);
            WindowPrefab<HotSeatSetupWindow>("HotSeatSetupWindow", popupIn, popupOut, true, BuildHotSeat);
            WindowPrefab<CampaignWindow>("CampaignWindow", popupIn, popupOut, true, BuildCampaign);
            WindowPrefab<ShopWindow>("ShopWindow", slideBottom, slideOutBottom, false, BuildShop);
            WindowPrefab<MatchHudWindow>("MatchHudWindow", fadeIn, fadeOut, false, BuildMatchHud);
            WindowPrefab<QuestsWindow>("QuestsWindow", popupIn, popupOut, true, BuildQuests);
            WindowPrefab<LeaderboardWindow>("LeaderboardWindow", popupIn, popupOut, true, BuildLeaderboard);
            WindowPrefab<TutorialTipWindow>("TutorialTipWindow", fadeIn, fadeOut, false, BuildTutorialTip);
            WindowPrefab<LanguageWindow>("LanguageWindow", popupIn, popupOut, true, BuildLanguage);
            WindowPrefab<ChallengeWindow>("ChallengeWindow", popupIn, popupOut, true, BuildChallenge);
            WindowPrefab<PerkChoiceWindow>("PerkChoiceWindow", popupIn, popupOut, true, BuildPerkChoice);
        }

        private static void WindowPrefab<T>(string name, UiTransitionConfig show, UiTransitionConfig hide, bool popup,
            System.Func<RectTransform, T> build) where T : UiWindow
        {
            Prefab(Prefabs + "/UI/" + name + ".prefab", () =>
            {
                var root = UiRect(name, null);
                Stretch(root);
                root.gameObject.AddComponent<CanvasGroup>();
                var window = build(root);
                window.EditorSetupTransitions(show, hide, popup);
                return root.gameObject;
            });
        }

        // ---------- Окна ----------

        private static MainMenuWindow BuildMainMenu(RectTransform root)
        {
            var panel = PanelAt(root);
            Corner(panel, new Vector2(0f, 0.5f), new Vector2(540f, 900f), new Vector2(48f, 0f));
            var column = Column(panel, 14, 26);

            var title = Text("Title", column, _ui.BoldFont, 52, _ui.Palette.Gold);
            Height(title, 150);
            var campaign = ButtonView("Campaign", column, 34, _ui.Palette.ButtonAccent);
            var endlessRun = ButtonView("EndlessRun", column, 32, _ui.Palette.ButtonAccent);
            var tower = ButtonView("Tower", column, 32, _ui.Palette.Button);
            var quests = ButtonView("Quests", column, 34, _ui.Palette.Button);
            var leaderboards = ButtonView("Leaderboards", column, 34, _ui.Palette.Button);
            var hotSeat = ButtonView("HotSeat", column, 34, _ui.Palette.Button);
            var shop = ButtonView("Shop", column, 34, _ui.Palette.Button);
            var settings = ButtonView("Settings", column, 30, _ui.Palette.ButtonMuted);
            foreach (var button in new[] { campaign, endlessRun, tower, quests, leaderboards, hotSeat, shop, settings })
                Height(button, 62);

            // Значок «есть награда» у правого края кнопки заданий.
            var badge = Box("Badge", quests.transform, _ui.Palette.Bad);
            Corner(badge, new Vector2(1f, 0.5f), new Vector2(44f, 44f), new Vector2(-12f, 0f));
            var badgeText = Text("Mark", badge, _ui.BoldFont, 30, _ui.Palette.Text);
            badgeText.text = "!";
            Stretch(badgeText.rectTransform);
            badge.gameObject.SetActive(false);

            // Книжка с вопросом в углу панели, вне раскладки колонки.
            var rules = ButtonView("Rules", panel, 48, Color.white);
            rules.Background.sprite = _ui.RulesIcon;
            rules.Background.preserveAspect = true;
            rules.SetText("?");
            Corner((RectTransform)rules.transform, new Vector2(1f, 1f), new Vector2(96f, 96f), new Vector2(-16f, -16f));

            Wallet(root);

            // Под кошельком: награды за рекламу (кнопка на строку GameConfig.MenuAdOffers) и особое предложение.
            var adOffers = UiRect("AdOffers", root);
            Corner(adOffers, new Vector2(1f, 1f), new Vector2(480f, 136f), new Vector2(-24f, -100f));
            var adLayout = adOffers.gameObject.AddComponent<VerticalLayoutGroup>();
            adLayout.spacing = 8;
            adLayout.childControlWidth = true;
            adLayout.childControlHeight = true;
            adLayout.childForceExpandHeight = false;
            var adTemplate = ButtonView("AdOfferTemplate", adOffers, 24, _ui.Palette.Bank);
            Height(adTemplate, 64);
            var offer = ButtonView("Offer", root, 26, _ui.Palette.ButtonAccent);
            Corner((RectTransform)offer.transform, new Vector2(1f, 1f), new Vector2(480f, 76f), new Vector2(-24f, -248f));

            var window = root.gameObject.AddComponent<MainMenuWindow>();
            window.EditorSetup(title, campaign, hotSeat, shop, settings, rules, quests, badge.gameObject, adOffers, adTemplate, offer, leaderboards,
                tower, endlessRun);
            return window;
        }

        private static SettingsWindow BuildSettings(RectTransform root)
        {
            Dim(root, 0.55f);
            var panel = CenterPanel(root, new Vector2(640f, 770f));
            var column = Column(panel, 18, 32);
            var title = Text("Title", column, _ui.BoldFont, 44, _ui.Palette.Gold);
            Height(title, 80);

            // Язык: флаг и название текущего языка, по нажатию — окно выбора.
            var language = Nest(LanguageButtonGameObjectPart(), column, "Language").GetComponent<LanguageButtonView>();
            Height(language, 80);
            var sound = ButtonView("Sound", column, 28, _ui.Palette.Button);
            var music = ButtonView("Music", column, 28, _ui.Palette.Button);
            var speed = ButtonView("Speed", column, 28, _ui.Palette.Button);
            var vibration = ButtonView("Vibration", column, 28, _ui.Palette.Button);
            var back = ButtonView("Back", column, 28, _ui.Palette.ButtonMuted);
            foreach (var button in new[] { sound, music, speed, vibration, back })
                Height(button, 72);

            var window = root.gameObject.AddComponent<SettingsWindow>();
            window.EditorSetup(title, sound, music, speed, back, language, vibration);
            return window;
        }

        private static ConfirmWindow BuildConfirm(RectTransform root)
        {
            Dim(root, 0.6f);
            var panel = CenterPanel(root, new Vector2(780f, 380f));
            var column = Column(panel, 24, 32);
            var text = Text("Text", column, _ui.Font, 32, _ui.Palette.Text);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = 32;
            Height(text, 170);
            var row = Row(column, 24);
            Height(row, 80);
            var yes = ButtonView("Yes", row, 30, _ui.Palette.ButtonAccent);
            var no = ButtonView("No", row, 30, _ui.Palette.ButtonMuted);

            var window = root.gameObject.AddComponent<ConfirmWindow>();
            window.EditorSetup(text, yes, no);
            return window;
        }

        private static StoryWindow BuildStory(RectTransform root)
        {
            Dim(root, 0.45f);
            var panel = PanelAt(root);
            Corner(panel, new Vector2(0.5f, 0f), new Vector2(1560f, 400f), new Vector2(0f, 40f));

            var portrait = UiRect("Portrait", panel).gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            Corner(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(300f, 300f), new Vector2(30f, 20f));
            portrait.gameObject.SetActive(false);

            var speaker = Text("Speaker", panel, _ui.BoldFont, 34, _ui.Palette.Gold);
            speaker.alignment = TextAlignmentOptions.Left;
            Anchor(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(-80f, 56f));
            var text = Text("Text", panel, _ui.Font, 30, _ui.Palette.Text);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = 30;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 10f), new Vector2(-80f, -200f));

            var buttons = Row(panel, 20);
            Corner(buttons, new Vector2(1f, 0f), new Vector2(560f, 68f), new Vector2(-32f, 24f));
            var skip = ButtonView("Skip", buttons, 24, _ui.Palette.ButtonMuted);
            var next = ButtonView("Next", buttons, 28, _ui.Palette.ButtonAccent);

            var window = root.gameObject.AddComponent<StoryWindow>();
            window.EditorSetup(speaker, text, portrait, skip, next);
            return window;
        }

        private static ResultsWindow BuildResults(RectTransform root)
        {
            Dim(root, 0.45f);
            var panel = CenterPanel(root, new Vector2(940f, 800f));
            var column = Column(panel, 14, 32);
            var title = Text("Title", column, _ui.BoldFont, 56, _ui.Palette.Gold);
            Height(title, 100);

            var lines = Column(column, 6, 0);
            lines.name = "Lines";
            var lineTemplate = Text("LineTemplate", lines, _ui.Font, 28, _ui.Palette.Text);
            lineTemplate.enableAutoSizing = true;
            lineTemplate.fontSizeMin = 18;
            lineTemplate.fontSizeMax = 28;
            Height(lineTemplate, 50);

            // Звёзды за соперника: копия детали Parts/Stars.
            var stars = Nest(StarsGameObjectPart(), column, "Stars").GetComponent<StarsView>();
            Height(stars, 48);

            var rewards = Column(column, 6, 0);
            rewards.name = "Rewards";
            var rewardTemplate = Text("RewardTemplate", rewards, _ui.BoldFont, 30, _ui.Palette.Good);
            Height(rewardTemplate, 44);

            var row = Row(column, 20);
            Height(row, 84);
            var menu = ButtonView("Menu", row, 30, _ui.Palette.ButtonMuted);
            var again = ButtonView("Again", row, 30, _ui.Palette.ButtonAccent);
            var doubleReward = ButtonView("Double", column, 28, _ui.Palette.Button);
            Height(doubleReward, 72);

            // Кошелёк в итогах: награды за партию видно, как монетки летят в него.
            Wallet(root);

            var window = root.gameObject.AddComponent<ResultsWindow>();
            window.EditorSetup(title, lines, lineTemplate, rewards, rewardTemplate, menu, again, doubleReward, stars);
            return window;
        }

        private static HotSeatSetupWindow BuildHotSeat(RectTransform root)
        {
            Dim(root, 0.35f);
            var panel = CenterPanel(root, new Vector2(1600f, 960f));
            var column = Column(panel, 14, 28);

            var title = Text("Title", column, _ui.BoldFont, 44, _ui.Palette.Gold);
            Height(title, 64);

            var targetRow = Row(column, 16);
            Height(targetRow, 72);
            var target = Text("Target", targetRow, _ui.Font, 32, _ui.Palette.Text);
            Width(target, -1, 1);
            var minus = ButtonView("Minus", targetRow, 40, _ui.Palette.ButtonMuted);
            var plus = ButtonView("Plus", targetRow, 40, _ui.Palette.ButtonMuted);
            Width(minus, 140, 0);
            Width(plus, 140, 0);

            var optionsRow = Row(column, 16);
            Height(optionsRow, 72);
            var first = ButtonView("FirstPlayer", optionsRow, 26, _ui.Palette.ButtonMuted);
            var special = ButtonView("SpecialDice", optionsRow, 26, _ui.Palette.ButtonMuted);

            var playersRow = Row(column, 24);
            Height(playersRow, 480);
            var players = new[] { PlayerPanel("Player1", playersRow), PlayerPanel("Player2", playersRow) };

            var buttons = Row(column, 24);
            Height(buttons, 84);
            var back = ButtonView("Back", buttons, 30, _ui.Palette.ButtonMuted);
            var start = ButtonView("Start", buttons, 34, _ui.Palette.ButtonAccent);

            var window = root.gameObject.AddComponent<HotSeatSetupWindow>();
            window.EditorSetup(title, target, minus, plus, first, special, players, back, start);
            return window;
        }

        private static PlayerSetupPanel PlayerPanel(string name, RectTransform parent)
        {
            var panel = Box(name, parent, _ui.Palette.PanelLight);
            var column = Column(panel, 12, 20);
            var title = Text("Title", column, _ui.BoldFont, 32, _ui.Palette.Text);
            title.alignment = TextAlignmentOptions.Left;
            Height(title, 48);
            var input = Input("Name", column);
            var diceLabel = Text("DiceLabel", column, _ui.Font, 22, _ui.Palette.TextMuted);
            diceLabel.alignment = TextAlignmentOptions.Left;
            Height(diceLabel, 34);
            var dice = DiceRow(column);
            var skin = ButtonView("Skin", column, 24, _ui.Palette.ButtonMuted);
            Height(skin, 64);

            var view = panel.gameObject.AddComponent<PlayerSetupPanel>();
            view.EditorSetup(title, input, diceLabel, dice, skin);
            return view;
        }

        private static CampaignWindow BuildCampaign(RectTransform root)
        {
            Dim(root, 0.35f);
            var panel = CenterPanel(root, new Vector2(1640f, 960f));
            var column = Column(panel, 12, 24);

            var header = Row(column, 16);
            Height(header, 72);
            var previous = ButtonView("PreviousChapter", header, 36, _ui.Palette.ButtonMuted);
            previous.SetText("<");
            Width(previous, 100, 0);
            var chapterTitle = Text("ChapterTitle", header, _ui.BoldFont, 36, _ui.Palette.Gold);
            Width(chapterTitle, -1, 1);
            var next = ButtonView("NextChapter", header, 36, _ui.Palette.ButtonMuted);
            next.SetText(">");
            Width(next, 100, 0);

            var body = Row(column, 20);
            Height(body, 470);
            // Список соперников: строки — префаб-деталь Parts/OpponentRow (вид настраивается в нём).
            var list = VerticalList("Opponents", body, 8, out var listContent);
            Width(list, -1, 1);
            var infoPanel = Box("Info", body, _ui.Palette.PanelLight);
            Width(infoPanel, -1, 1);
            // Портрет соперника в правом верхнем углу, текст слева от него, условия звёзд строками внизу.
            var portrait = UiRect("Portrait", infoPanel).gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            Corner(portrait.rectTransform, new Vector2(1f, 1f), new Vector2(180f, 180f), new Vector2(-16f, -16f));
            var info = Text("InfoText", infoPanel, _ui.Font, 26, _ui.Palette.Text);
            info.alignment = TextAlignmentOptions.TopLeft;
            info.enableAutoSizing = true;
            info.fontSizeMin = 16;
            info.fontSizeMax = 26;
            Anchor(info.rectTransform, Vector2.zero, Vector2.one, new Vector2(-98f, 75f), new Vector2(-236f, -190f));

            var conditions = UiRect("StarConditions", infoPanel);
            conditions.anchorMin = new Vector2(0f, 0f);
            conditions.anchorMax = new Vector2(1f, 0f);
            conditions.pivot = new Vector2(0.5f, 0f);
            conditions.sizeDelta = new Vector2(-40f, 160f);
            conditions.anchoredPosition = new Vector2(0f, 16f);
            var conditionsLayout = conditions.gameObject.AddComponent<VerticalLayoutGroup>();
            conditionsLayout.spacing = 2;
            conditionsLayout.childControlWidth = true;
            conditionsLayout.childControlHeight = true;
            conditionsLayout.childForceExpandHeight = false;
            var conditionsTitle = Text("Title", conditions, _ui.BoldFont, 22, _ui.Palette.Text);
            conditionsTitle.alignment = TextAlignmentOptions.Left;
            Height(conditionsTitle, 30);

            var diceHeader = Row(column, 12);
            diceHeader.name = "DiceHeader";
            Height(diceHeader, 48);
            var myDiceLabel = Text("MyDiceLabel", diceHeader, _ui.Font, 22, _ui.Palette.TextMuted);
            myDiceLabel.alignment = TextAlignmentOptions.Left;
            myDiceLabel.enableAutoSizing = true;
            myDiceLabel.fontSizeMin = 16;
            myDiceLabel.fontSizeMax = 22;
            Width(myDiceLabel, -1, 1);
            var presetsRow = Row(diceHeader, 8);
            presetsRow.name = "Presets";
            Width(presetsRow, 480, 0);
            var presetTemplate = ButtonView("PresetTemplate", presetsRow, 20, _ui.Palette.ButtonMuted);
            var presets = presetsRow.gameObject.AddComponent<DicePresetsView>();
            presets.EditorSetup(presetsRow, presetTemplate);
            var myDice = DiceRow(column);

            // Ставка монетами на партию.
            var stakeRow = Row(column, 10);
            stakeRow.name = "Stake";
            Height(stakeRow, 56);
            var stakeLabel = Text("StakeLabel", stakeRow, _ui.Font, 22, _ui.Palette.TextMuted);
            stakeLabel.alignment = TextAlignmentOptions.Left;
            stakeLabel.enableAutoSizing = true;
            stakeLabel.fontSizeMin = 16;
            stakeLabel.fontSizeMax = 22;
            Width(stakeLabel, 200, 0);
            var stakeList = Row(stakeRow, 8);
            stakeList.name = "StakeOptions";
            Width(stakeList, -1, 1);
            var stakeTemplate = ButtonView("StakeTemplate", stakeList, 22, _ui.Palette.ButtonMuted);

            var buttons = Row(column, 24);
            Height(buttons, 84);
            var back = ButtonView("Back", buttons, 30, _ui.Palette.ButtonMuted);
            var play = ButtonView("Play", buttons, 32, _ui.Palette.ButtonAccent);

            Wallet(root);
            var window = root.gameObject.AddComponent<CampaignWindow>();
            window.EditorSetup(previous, next, chapterTitle, listContent, null, info, myDiceLabel, myDice, back, play, presets,
                stakeLabel, stakeList, stakeTemplate, portrait, OpponentRowPart(), conditions, StarConditionRowPart(), conditionsTitle);
            return window;
        }

        private static ShopWindow BuildShop(RectTransform root)
        {
            var title = Text("Title", root, _ui.BoldFont, 40, _ui.Palette.Gold);
            Corner(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 80f), new Vector2(0f, -24f));

            // Нижняя панель на всю ширину с отступами по краям.
            var panel = PanelAt(root);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(-48f, 360f);
            panel.anchoredPosition = new Vector2(0f, 16f);
            var column = Column(panel, 10, 14);

            var tabs = Row(column, 8);
            Height(tabs, 64);
            var tabTemplate = ButtonView("TabTemplate", tabs, 24, _ui.Palette.ButtonMuted);
            var back = ButtonView("Back", tabs, 24, _ui.Palette.Button);

            var body = Row(column, 16);
            Height(body, 250);
            var items = HorizontalList("Items", body, 10, out var itemsContent);
            Width(items, -1, 3);

            var detailsColumn = Column(body, 8, 8);
            Width(detailsColumn, 520, 0);
            var itemName = Text("ItemName", detailsColumn, _ui.BoldFont, 30, _ui.Palette.Gold);
            itemName.alignment = TextAlignmentOptions.Left;
            Height(itemName, 44);
            var details = Column(detailsColumn, 8, 0);
            details.name = "Details";
            var detailButton = ButtonView("DetailButtonTemplate", details, 24, _ui.Palette.Button);
            Height(detailButton, 60);
            var detailText = Text("DetailTextTemplate", details, _ui.Font, 20, _ui.Palette.TextMuted);
            detailText.alignment = TextAlignmentOptions.TopLeft;
            Height(detailText, 48);

            Wallet(root);
            var window = root.gameObject.AddComponent<ShopWindow>();
            window.EditorSetup(title, tabs, tabTemplate, back, itemsContent, null, itemName, details, detailButton, detailText, ShopCardPart());
            return window;
        }

        private static MatchHudWindow BuildMatchHud(RectTransform root)
        {
            var players = new[]
            {
                HudPanel("Player1", root, new Vector2(0f, 1f), new Vector2(24f, -24f)),
                HudPanel("Player2", root, new Vector2(1f, 1f), new Vector2(-24f, -24f)),
            };

            var turn = Text("Turn", root, _ui.BoldFont, 32, _ui.Palette.Text);
            Corner(turn.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 110f), new Vector2(0f, -20f));
            turn.outlineColor = new Color(0f, 0f, 0f, 0.8f);
            turn.outlineWidth = 0.2f;

            var hint = Text("Hint", root, _ui.Font, 30, _ui.Palette.Text);
            Corner(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(1100f, 60f), new Vector2(0f, 130f));
            hint.outlineColor = new Color(0f, 0f, 0f, 0.8f);
            hint.outlineWidth = 0.2f;

            var buttons = Row(root, 24);
            Corner(buttons, new Vector2(0.5f, 0f), new Vector2(780f, 96f), new Vector2(0f, 24f));
            var roll = ButtonView("Roll", buttons, 34, _ui.Palette.ButtonAccent);
            var bank = ButtonView("Bank", buttons, 30, _ui.Palette.Bank);

            var surrender = ButtonView("Surrender", root, 22, _ui.Palette.ButtonMuted);
            Corner((RectTransform)surrender.transform, new Vector2(1f, 0f), new Vector2(220f, 64f), new Vector2(-24f, 24f));
            var phrasesButton = ButtonView("Phrases", root, 22, _ui.Palette.ButtonMuted);
            Corner((RectTransform)phrasesButton.transform, new Vector2(0f, 0f), new Vector2(220f, 64f), new Vector2(24f, 24f));

            var phrases = Box("PhrasesPanel", root, _ui.Palette.Panel);
            Corner(phrases, new Vector2(0f, 0f), new Vector2(380f, 100f), new Vector2(24f, 100f));
            var phrasesLayout = phrases.gameObject.AddComponent<VerticalLayoutGroup>();
            phrasesLayout.spacing = 8;
            phrasesLayout.padding = new RectOffset(10, 10, 10, 10);
            phrasesLayout.childControlWidth = true;
            phrasesLayout.childControlHeight = true;
            phrasesLayout.childForceExpandHeight = false;
            phrases.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var phraseTemplate = ButtonView("PhraseTemplate", phrases, 22, _ui.Palette.Button);
            Height(phraseTemplate, 58);

            var window = root.gameObject.AddComponent<MatchHudWindow>();
            window.EditorSetup(players, turn, hint, roll, bank, surrender, phrasesButton, phrases, phraseTemplate);
            return window;
        }

        private static QuestsWindow BuildQuests(RectTransform root)
        {
            Dim(root, 0.5f);
            var panel = CenterPanel(root, new Vector2(1400f, 920f));
            var column = Column(panel, 12, 28);

            var header = Row(column, 16);
            Height(header, 72);
            var title = Text("Title", header, _ui.BoldFont, 44, _ui.Palette.Gold);
            title.alignment = TextAlignmentOptions.Left;
            Width(title, -1, 1);
            var daily = ButtonView("DailyTab", header, 26, _ui.Palette.ButtonAccent);
            var weekly = ButtonView("WeeklyTab", header, 26, _ui.Palette.ButtonMuted);
            Width(daily, 260, 0);
            Width(weekly, 260, 0);

            var timer = Text("Timer", column, _ui.Font, 24, _ui.Palette.TextMuted);
            timer.alignment = TextAlignmentOptions.Left;
            Height(timer, 40);

            var list = VerticalList("Quests", column, 10, out var listContent);
            Height(list, 590);
            var row = QuestRowPart();

            var buttons = Row(column, 24);
            Height(buttons, 80);
            var back = ButtonView("Back", buttons, 30, _ui.Palette.ButtonMuted);
            Width(back, 360, 0);
            buttons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var window = root.gameObject.AddComponent<QuestsWindow>();
            window.EditorSetup(title, daily, weekly, timer, listContent, row, back);
            return window;
        }

        /// <summary>Выбор языка: сетка кнопок-деталей Parts/LanguageButton (флаг и название).</summary>
        private static LanguageWindow BuildLanguage(RectTransform root)
        {
            Dim(root, 0.6f);
            var panel = CenterPanel(root, new Vector2(1000f, 760f));
            var column = Column(panel, 16, 32);
            var title = Text("Title", column, _ui.BoldFont, 40, _ui.Palette.Gold);
            Height(title, 64);

            var list = UiRect("Languages", column);
            Height(list, 480);
            var grid = list.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(290f, 90f);
            grid.spacing = new Vector2(16f, 16f);
            grid.childAlignment = TextAnchor.UpperCenter;

            var buttons = Row(column, 24);
            Height(buttons, 80);
            buttons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var close = ButtonView("Close", buttons, 30, _ui.Palette.ButtonMuted);
            Width(close, 360, 0);

            var window = root.gameObject.AddComponent<LanguageWindow>();
            window.EditorSetup(title, list, LanguageButtonGameObjectPart().GetComponent<LanguageButtonView>(), close);
            return window;
        }

        /// <summary>Подсказка обучения: табличка сверху под счётом, без затемнения — нажатия мимо проходят в игру.</summary>
        private static TutorialTipWindow BuildTutorialTip(RectTransform root)
        {
            var panel = PanelAt(root);
            Corner(panel, new Vector2(0.5f, 1f), new Vector2(1180f, 170f), new Vector2(0f, -150f));
            var text = Text("Text", panel, _ui.Font, 28, _ui.Palette.Text);
            text.alignment = TextAlignmentOptions.Left;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18;
            text.fontSizeMax = 28;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(-120f, 0f), new Vector2(-280f, -24f));
            var ok = ButtonView("Ok", panel, 26, _ui.Palette.ButtonAccent);
            Corner((RectTransform)ok.transform, new Vector2(1f, 0.5f), new Vector2(200f, 72f), new Vector2(-20f, 0f));

            var window = root.gameObject.AddComponent<TutorialTipWindow>();
            window.EditorSetup(text, ok);
            return window;
        }

        private static LeaderboardWindow BuildLeaderboard(RectTransform root)
        {
            Dim(root, 0.5f);
            var panel = CenterPanel(root, new Vector2(1100f, 920f));
            var column = Column(panel, 12, 28);

            var title = Text("Title", column, _ui.BoldFont, 44, _ui.Palette.Gold);
            Height(title, 64);

            var tabs = Row(column, 12);
            Height(tabs, 64);
            var tabTemplate = ButtonView("TabTemplate", tabs, 24, _ui.Palette.ButtonMuted);

            var status = Text("Status", column, _ui.Font, 24, _ui.Palette.TextMuted);
            Height(status, 36);

            var list = VerticalList("Rows", column, 6, out var listContent);
            Height(list, 520);

            var me = Text("Me", column, _ui.BoldFont, 28, _ui.Palette.Gold);
            me.enableAutoSizing = true;
            me.fontSizeMin = 18;
            me.fontSizeMax = 28;
            Height(me, 50);

            var buttons = Row(column, 24);
            Height(buttons, 80);
            buttons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var back = ButtonView("Back", buttons, 30, _ui.Palette.ButtonMuted);
            Width(back, 360, 0);

            var window = root.gameObject.AddComponent<LeaderboardWindow>();
            window.EditorSetup(title, tabs, tabTemplate, status, listContent, null, me, back, LeaderboardRowPart());
            return window;
        }

        /// <summary>Строка задания: слева текст, полоса прогресса и награда, справа «Забрать» и «Заменить».</summary>
        private static QuestRowView QuestRow(RectTransform parent)
        {
            var row = Box("QuestRowTemplate", parent, _ui.Palette.PanelLight);
            Height(row, 150);

            var title = Text("Title", row, _ui.BoldFont, 28, _ui.Palette.Text);
            title.alignment = TextAlignmentOptions.Left;
            title.enableAutoSizing = true;
            title.fontSizeMin = 18;
            title.fontSizeMax = 28;
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -44f), new Vector2(-400f, 56f));

            var bar = Box("Bar", row, new Color(0f, 0f, 0f, 0.45f));
            Anchor(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -93f), new Vector2(-400f, 26f));
            var fill = Box("Fill", bar, _ui.Palette.Good);
            Stretch(fill);
            var progress = Text("Progress", bar, _ui.BoldFont, 20, _ui.Palette.Text);
            Stretch(progress.rectTransform);
            progress.outlineColor = new Color(0f, 0f, 0f, 0.8f);
            progress.outlineWidth = 0.2f;

            var reward = Text("Reward", row, _ui.Font, 22, _ui.Palette.Gold);
            reward.alignment = TextAlignmentOptions.Left;
            reward.enableAutoSizing = true;
            reward.fontSizeMin = 16;
            reward.fontSizeMax = 22;
            Anchor(reward.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -128f), new Vector2(-400f, 32f));

            var claim = ButtonView("Claim", row, 26, _ui.Palette.ButtonAccent);
            Corner((RectTransform)claim.transform, new Vector2(1f, 0.5f), new Vector2(340f, 62f), new Vector2(-20f, 32f));
            var reroll = ButtonView("Reroll", row, 20, _ui.Palette.Button);
            Corner((RectTransform)reroll.transform, new Vector2(1f, 0.5f), new Vector2(340f, 50f), new Vector2(-20f, -32f));

            var view = row.gameObject.AddComponent<QuestRowView>();
            view.EditorSetup(title, progress, fill, reward, claim, reroll);
            return view;
        }

        /// <summary>Табличка игрока с нуля: так строится образец Parts/HudPlayerPanel.</summary>
        private static HudPlayerPanel CreateHudPanel(Transform parent)
        {
            var panel = Box("HudPlayerPanel", parent, _ui.Palette.Panel);
            panel.sizeDelta = new Vector2(480f, 120f);

            // Портрет слева (у соперника), имя и счёт правее.
            var portrait = UiRect("Portrait", panel).gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            Corner(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(100f, 100f), new Vector2(10f, 0f));
            portrait.gameObject.SetActive(false);

            var column = Column(panel, 2, 10);
            column.offsetMin = new Vector2(116f, 0f);
            var playerName = Text("Name", column, _ui.BoldFont, 28, _ui.Palette.Text);
            playerName.enableAutoSizing = true;
            playerName.fontSizeMin = 18;
            playerName.fontSizeMax = 28;
            Height(playerName, 40);
            var score = Text("Score", column, _ui.BoldFont, 40, _ui.Palette.Gold);
            Height(score, 56);

            var view = panel.gameObject.AddComponent<HudPlayerPanel>();
            view.EditorSetup(panel.GetComponent<Image>(), playerName, score, portrait);
            return view;
        }

        // ---------- Общие детали ----------

        /// <summary>Кошелёк с нуля: так строится образец Parts/Wallet. В окнах — Wallet (копия образца).</summary>
        private static RectTransform CreateWallet(Transform parent)
        {
            var panel = Box("Wallet", parent, _ui.Palette.Panel);
            panel.sizeDelta = new Vector2(480f, 64f);
            var row = Row(panel, 16);
            Stretch(row);
            var coins = Text("Coins", row, _ui.BoldFont, 26, _ui.Palette.Gold);
            var energy = Text("Energy", row, _ui.BoldFont, 26, _ui.Palette.Good);
            foreach (var text in new[] { coins, energy })
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = 16;
                text.fontSizeMax = 26;
            }

            panel.gameObject.AddComponent<WalletView>().EditorSetup(coins, energy);
            return panel;
        }

        private static DiceLoadoutView DiceRow(RectTransform parent)
        {
            var row = Row(parent, 8);
            row.name = "Dice";
            Height(row, 84);
            var slots = new UiButtonView[6];
            for (var i = 0; i < slots.Length; i++)
                slots[i] = ButtonView("Die" + (i + 1), row, 18, _ui.Palette.ButtonMuted);

            var view = row.gameObject.AddComponent<DiceLoadoutView>();
            view.EditorSetup(slots);
            return view;
        }

        /// <summary>Кнопка с нуля: так строится образец Parts/Button. В окнах — ButtonView (копия образца).</summary>
        private static UiButtonView CreateButton(string name, Transform parent, float fontSize, Color color)
        {
            var rect = UiRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            var label = Text("Label", rect, _ui.BoldFont, fontSize, _ui.Palette.Text);
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(16f, fontSize);
            label.fontSizeMax = fontSize;
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16f, -8f));

            var view = rect.gameObject.AddComponent<UiButtonView>();
            view.EditorSetup(button, label, image);
            return view;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            return UiText(name, parent, font, size, color);
        }

        private static TMP_InputField Input(string name, RectTransform parent)
        {
            var background = Box(name, parent, new Color(1f, 1f, 1f, 0.12f));
            Height(background, 56);
            var area = UiRect("TextArea", background);
            Anchor(area, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24f, -8f));
            area.gameObject.AddComponent<RectMask2D>();
            var text = Text("Text", area, _ui.Font, 26, _ui.Palette.Text);
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false;
            Stretch(text.rectTransform);

            var input = background.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.fontAsset = _ui.Font;
            input.characterLimit = 16;
            return input;
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        {
            var rect = UiRect(name, parent);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        /// <summary>Подложка окна по центру: копия образца Parts/WindowPanel (спрайт и цвет — в образце).</summary>
        private static RectTransform CenterPanel(RectTransform root, Vector2 size)
        {
            var panel = PanelAt(root);
            Place(panel, new Vector2(0.5f, 0.5f), size);
            return panel;
        }

        private static void Dim(RectTransform root, float alpha)
        {
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, alpha);
        }

        /// <summary>Элемент у угла или края: якорь и точка опоры совпадают, отступ от этого угла.</summary>
        private static void Corner(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static RectTransform Column(RectTransform parent, float spacing, int padding)
        {
            var rect = UiRect("Column", parent);
            if (parent.GetComponent<LayoutGroup>() == null)
                Stretch(rect);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private static RectTransform Row(RectTransform parent, float spacing)
        {
            var rect = UiRect("Row", parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return rect;
        }

        private static RectTransform VerticalList(string name, RectTransform parent, float spacing, out RectTransform content)
        {
            var root = Box(name, parent, new Color(1f, 1f, 1f, 0.03f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = UiRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return root;
        }

        private static RectTransform HorizontalList(string name, RectTransform parent, float spacing, out RectTransform content)
        {
            var root = Box(name, parent, new Color(1f, 1f, 1f, 0.03f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = UiRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return root;
        }

        private static void Height(Component component, float height)
        {
            var layout = component.GetComponent<LayoutElement>();
            if (layout == null)
                layout = component.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        /// <summary>Ширина в строке: width ≥ 0 — фиксированная, flexible — доля свободного места.</summary>
        private static void Width(Component component, float width, float flexible)
        {
            var layout = component.GetComponent<LayoutElement>();
            if (layout == null)
                layout = component.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
            }

            layout.flexibleWidth = flexible;
        }
    }
}
