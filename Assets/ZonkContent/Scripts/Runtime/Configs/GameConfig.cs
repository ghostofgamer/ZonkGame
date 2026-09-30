using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>
    /// Общие настройки игры. Один ассет, подключён в ZonkInstaller. Здесь всё, что не является
    /// отдельным контентом: стартовые значения, пределы, тайминги.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Интерфейс")]
        public Zonk.UI.UiConfig Ui;

        [Header("Режимы")]
        public GameModeConfig HotSeatMode;
        public GameModeConfig CampaignMode;

        [Header("Валюты")]
        public CurrencyConfig Coins;
        public CurrencyConfig Energy;

        [Header("Кости")]
        public DieConfig StandardDie;

        [Tooltip("Сколько особых костей можно взять в набор из шести. Каждая особая кость — не больше одной в наборе")]
        public int MaxSpecialDice = 6;

        [Tooltip("Сколько особых костей может быть у обычного соперника кампании (проверяет Zonk/Content/Validate)")]
        public int OpponentMaxSpecialDice = 3;

        [Tooltip("Сколько особых костей должно быть у босса, не меньше (проверяет Zonk/Content/Validate)")]
        public int BossMinSpecialDice = 3;

        [Tooltip("Сколько наборов костей можно сохранить и переключать одной кнопкой")]
        public int DicePresetCount = 3;

        [Header("Мастерство костей")]
        [Tooltip("Уровни мастерства особых костей по возрастанию очков")]
        public List<MasteryLevel> MasteryLevels = new List<MasteryLevel>();

        [Header("Ставки в кампании")]
        [Tooltip("Варианты ставки монетами перед партией кампании. 0 = без ставки")]
        public int[] StakeOptions = { 0, 50, 100, 250, 500 };

        [Header("Награды за рекламу в главном меню")]
        [Tooltip("Кнопка на каждую строку: валюта, сколько за просмотр и сколько раз в день")]
        public List<MenuAdOffer> MenuAdOffers = new List<MenuAdOffer>();

        [Header("Звёзды за соперников")]
        [Tooltip("Награда за каждую новую звезду")]
        [SerializeReference, SubclassSelector]
        public List<Reward> NewStarRewards = new List<Reward>();

        [Header("Обучение")]
        public TutorialConfig Tutorial;

        [Header("Задания")]
        [Tooltip("Сколько заданий выдаётся на день")]
        public int DailyQuestCount = 3;

        [Tooltip("Сколько заданий выдаётся на неделю")]
        public int WeeklyQuestCount = 3;

        [Tooltip("Сколько раз в день можно заменить дневное задание за рекламу")]
        public int QuestRerollsPerDay = 1;

        [Header("Игра вдвоём")]
        public int HotSeatMinTarget = 1000;
        public int HotSeatMaxTarget = 10000;
        public int HotSeatTargetStep = 500;
        public int HotSeatDefaultTarget = 4000;

        [Tooltip("Скин второго игрока, если оба выбрали одинаковый")]
        public CosmeticItemConfig SecondPlayerFallbackSkin;

        public Color[] PlayerColors = { new Color(0.95f, 0.75f, 0.3f), new Color(0.4f, 0.75f, 0.95f) };

        [Header("Реакции")]
        [Tooltip("Ход от стольких очков считается крупным")]
        public int BigBankScore = 1000;

        [Tooltip("Отложенная за раз комбинация от стольких очков считается крупной: соперник может ударить по столу")]
        public int BigKeepScore = 1000;

        [Tooltip("Бросок при стольких очках хода и 1–2 костях считается рискованным")]
        public int RiskyRollScore = 500;

        [Tooltip("Версия раскладки цен магазина, которую применил генератор. Руками не менять")]
        public int ShopLayoutVersion;

        [Header("Освещение")]
        [Tooltip("Освещение локации без своего профиля")]
        public LightingProfileConfig DefaultLighting;

        [Header("Стол и бросок")]
        [Tooltip("Ребро кости в метрах сцены. Модель 0.3, меньше = кости мельче относительно стакана и лотка")]
        public float DieSize = 0.24f;

        [Tooltip("Стиль броска по умолчанию: тряска и высыпание")]
        public RollStyleConfig RollStyle;

        [Tooltip("Бросок своей рукой: игрок трясёт стакан и толкает его к столу")]
        public ManualRollConfig ManualRoll;

        [Tooltip("Ощущение партии: всплывающие очки, накрутка счёта, замедление, искры, пыль, вибрация")]
        public MatchFeelConfig Feel;

        [Header("Анимация")]
        public float SettleDuration = 0.35f;
        public float KeepDuration = 0.3f;
    }
}
