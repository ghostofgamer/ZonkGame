using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>Режим игры: правила, стоимость входа, награды, повод для межстраничной рекламы.</summary>
    [CreateAssetMenu(menuName = "Zonk/Game Mode", fileName = "Mode")]
    public sealed class GameModeConfig : ContentConfig
    {
        public string DescriptionKey;
        public RuleSetConfig Rules;

        [Tooltip("Энергии за партию. 0 = бесплатно")]
        public int EnergyCost;

        [Tooltip("Можно ли брать особые кости")]
        public bool AllowSpecialDice = true;

        [Tooltip("Повод для IInterstitialService после партии. Пусто = без рекламы")]
        public string InterstitialTrigger = "match_end";

        [SerializeReference, SubclassSelector]
        public List<Reward> WinRewards = new List<Reward>();
    }
}
