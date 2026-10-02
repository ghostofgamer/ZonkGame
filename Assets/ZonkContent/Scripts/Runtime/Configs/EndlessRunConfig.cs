using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Modes;
using Zonk.Core.Modifiers;

namespace Zonk.Configs
{
    /// <summary>
    /// «Бесконечный забег» (в коде — Roguelike): этажи со случайным соперником, враги растут с этажом, игрок — от находок.
    /// Проигрыш забирает сердце, сердца кончились — конец забега (один раз за забег можно продолжить за рекламу).
    /// Рекорд — лучший этаж. Числа роста этажа — Plan (подобраны моделью, см. EndlessRunPlan).
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Endless Run", fileName = "EndlessRun")]
    public sealed class EndlessRunConfig : ScriptableObject
    {
        [Tooltip("Как растут этажи: цель, сила врага, его кости и ИИ, стражи, общие правила")]
        public EndlessRunPlan Plan = new EndlessRunPlan();

        [Header("Игрок")]
        [Tooltip("Энергия за начало забега (этажи внутри бесплатны)")]
        public int EnergyCost = 1;

        public int StartHearts = 3;

        [Tooltip("Больше сердец не бывает: находка «сердце» не предлагается")]
        public int MaxHearts = 5;

        [Tooltip("Сердец за победу над стражем")]
        public int HeartsPerGuardian = 1;

        [Tooltip("Один раз за забег продолжить за рекламу (+1 сердце), когда сердца кончились")]
        public bool ReviveForAd = true;

        [Header("Находки после победы")]
        [Tooltip("Сколько находок на выбор")]
        public int Offers = 3;

        [Tooltip("Находка «очки комбинации»: на сколько растёт множитель очков категории")]
        public float ComboStep = 0.3f;

        [Tooltip("Вес находок: очки комбинации / особая кость / сердце")]
        public int ComboWeight = 5;
        public int DieWeight = 3;
        public int HeartWeight = 2;

        [Header("Враги")]
        [Tooltip("Обычные соперники этажей: берутся их имя, портрет, стакан и реплики; кости и ИИ задаёт забег")]
        public List<OpponentConfig> Opponents = new List<OpponentConfig>();

        [Tooltip("Стражи (каждый Plan.GuardianEvery-й этаж): боссы; к правилам этажа добавляются их правила")]
        public List<OpponentConfig> Guardians = new List<OpponentConfig>();

        [Tooltip("ИИ врага по этапам: до Plan.EarlyUntil, до Plan.MidUntil, дальше")]
        public AiProfileConfig AiEarly;
        public AiProfileConfig AiMid;
        public AiProfileConfig AiLate;

        [Tooltip("Особые кости: для врагов и находки «особая кость»")]
        public List<DieConfig> SpecialDice = new List<DieConfig>();

        [Tooltip("Общие правила, которые появляются каждые Plan.RulesEvery этажей (действуют на обоих)")]
        [SerializeReference, SubclassSelector]
        public List<MatchModifier> RulePool = new List<MatchModifier>();

        [Header("Награды")]
        [Tooltip("Монет за каждый пройденный этаж")]
        public int CoinsPerFloor = 8;

        [Tooltip("Дополнительно монет за стража")]
        public int GuardianCoins = 40;

        [Tooltip("Рубежи: награда один раз за всё время, когда впервые дошёл до этажа")]
        public List<RunMilestone> Milestones = new List<RunMilestone>();

        [Tooltip("Прирост монет за этаж: на этаже N — CoinsPerFloor + N × это")]
        public float CoinsGrowthPerFloor = 0.5f;

        [Tooltip("Награду рубежа можно удвоить за рекламу")]
        public bool MilestoneDoubleForAd = true;

        [Header("Находки 2.0")]
        [Tooltip("Пул находок: редкость, вес, стаки, тактика, поведение. Пусто — старые находки (очки, кость, сердце)")]
        public List<RunPerkEntry> Perks = new List<RunPerkEntry>();

        [Header("Развилки и лавка")]
        [Tooltip("Привал или лавка на выбор — каждые столько этажей (чередуются)")]
        [Min(0)] public int CampEvery = 3;

        [Tooltip("Сильный соперник: прибавка к силе очков врага")]
        public float EliteEnemyPower = 0.15f;

        [Tooltip("Сильный соперник: особых костей больше на")]
        public int EliteExtraDice = 1;

        [Tooltip("Сильный соперник: цель выше на, %")]
        public int EliteTargetPercent = 10;

        [Tooltip("Сильный соперник: монет больше на, %")]
        public int EliteCoinsPercent = 50;

        [Tooltip("Жетонов за этаж: базовые и ещё по одному за каждые 10 этажей")]
        public int TokensPerFloor = 1;

        public int EliteTokens = 2;

        [Tooltip("Цены лавки в жетонах: обычная, редкая, легендарная")]
        public int[] ShopPrices = { 3, 6, 10 };

        [Header("Реклама")]
        [Tooltip("Сколько раз за забег продолжить за рекламу")]
        [Min(0)] public int MaxRevives = 3;

        [Tooltip("Второе и следующие продолжения — только с этого этажа")]
        public int DeepReviveFloor = 10;

        [Tooltip("Межстраничная реклама после партии — раз в столько этажей (частоту держит общий слой)")]
        [Min(1)] public int InterstitialEvery = 2;
    }

    [Serializable]
    public sealed class RunMilestone
    {
        public int Floor = 10;

        [SerializeReference, SubclassSelector]
        public List<Reward> Rewards = new List<Reward>();
    }
}
