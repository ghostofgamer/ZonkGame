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

        public AiProfileConfig Ai;

        [Tooltip("Кости соперника по слотам. Пусто или меньше 6 = остальные обычные")]
        public List<DieConfig> Dice = new List<DieConfig>();

        [Tooltip("Скин костей соперника. Пусто = базовый")]
        public CosmeticItemConfig DiceSkin;

        [Tooltip("Стакан соперника. Пусто = базовый")]
        public CosmeticItemConfig Cup;

        [Tooltip("Цель партии с этим соперником. 0 = из правил режима")]
        public int TargetScore;

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
