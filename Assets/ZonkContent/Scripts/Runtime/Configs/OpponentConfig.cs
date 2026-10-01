using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>
    /// Соперник кампании. Обычный: имя, тактика, реплики. Босс: плюс особое правило (модификатор),
    /// своя кость в награду. Правило действует на обоих игроков и показывается до партии.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Opponent", fileName = "Opponent")]
    public sealed class OpponentConfig : ContentConfig
    {
        [Tooltip("Прозвище под именем: «Боцман», «Монах»")]
        public string TitleKey;

        [Tooltip("Описание особого правила босса, показывается перед партией")]
        public string RuleKey;

        public Sprite Portrait;

        [Tooltip("Цвет силуэта заглушки")]
        public Color BodyColor = new Color(0.25f, 0.25f, 0.3f);

        [Tooltip("Аксессуар на голову заглушки: шляпа, повязка")]
        public GameObject Accessory;

        public bool IsBoss;

        [Header("Грозная версия босса")]
        [Tooltip("Этот соперник — грозная версия босса DreadOf: тот же персонаж, свои правила, кости, цель и награды. " +
                 "Кладётся в ChapterConfig.DreadBosses, не в Opponents. Пусто — обычный соперник")]
        public OpponentConfig DreadOf;

        [Tooltip("Грозная версия открывается, когда у босса DreadOf столько звёзд (не больше, чем у него бывает)")]
        public int DreadUnlockStars = 3;

        public bool IsDread => DreadOf != null;

        /// <summary>Сколько звёзд у обычного босса нужно, чтобы открылась эта грозная версия.</summary>
        public int DreadStarsRequired => DreadOf == null ? 0 : Math.Min(Math.Max(1, DreadUnlockStars), 1 + DreadOf.StarConditions.Count);

        public AiProfileConfig Ai;

        [Tooltip("Кости соперника по слотам. Пусто или меньше 6 = остальные обычные")]
        public List<DieConfig> Dice = new List<DieConfig>();

        [Tooltip("Скин костей соперника. Пусто = базовый")]
        public CosmeticItemConfig DiceSkin;

        [Tooltip("Стакан соперника. Пусто = базовый")]
        public CosmeticItemConfig Cup;

        [Tooltip("Цель партии с этим соперником, любая (у нынешних боссов 10000, можно и 340000). 0 = из правил режима")]
        public int TargetScore;

        /// <summary>Цель партии с этим соперником: своя из конфига, иначе цель режима modeTarget. Везде брать цель отсюда.</summary>
        public int TargetFor(int modeTarget)
        {
            return TargetScore > 0 ? TargetScore : modeTarget;
        }

        [Tooltip("Условия дополнительных звёзд (первая звезда — победа). Проверяются только при победе")]
        [SerializeReference, SubclassSelector]
        public List<StarCondition> StarConditions = new List<StarCondition>();

        [Tooltip("Ставка: сколько чистыми получает игрок за победу на каждую поставленную монету. Меньше 1 у слабых " +
                 "соперников, чтобы ставки на лёгкие победы не стали фермой монет (по победам ИИ в Zonk/Balance Simulator)")]
        public float StakePayout = 1f;

        [SerializeReference, SubclassSelector]
        public List<MatchModifier> Modifiers = new List<MatchModifier>();

        public ReactionSetConfig Reactions;

        [Tooltip("Свой стиль броска: как соперник трясёт стакан. Пусто = стиль по умолчанию")]
        public RollStyleConfig RollStyle;

        [Tooltip("Энергия за партию. -1 = как в режиме")]
        public int EnergyCost = -1;

        [SerializeReference, SubclassSelector]
        public List<Reward> FirstWinRewards = new List<Reward>();

        [SerializeReference, SubclassSelector]
        public List<Reward> RepeatWinRewards = new List<Reward>();
    }

    [Serializable]
    public sealed class StoryLine
    {
        public string SpeakerKey;
        public Sprite Portrait;
        public string TextKey;
    }
}
