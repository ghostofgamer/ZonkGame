using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>
    /// Характер ИИ: тактика выбора костей и риска. Обычные соперники различаются в основном этим.
    /// Случайность у всех честная: ИИ не видит будущих бросков.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/AI Profile", fileName = "AiProfile")]
    public sealed class AiProfileConfig : ContentConfig
    {
        [SerializeReference, SubclassSelector]
        public AiSelectionPolicy Selection = new GreedySelection();

        [SerializeReference, SubclassSelector]
        public AiRiskPolicy Risk = new ThresholdRisk();

        [Range(0f, 0.5f)]
        public float MistakeChance;

        [Tooltip("Пауза «на подумать» перед решением, секунды")]
        public Vector2 ThinkDelay = new Vector2(0.6f, 1.4f);

        public AiProfile ToProfile()
        {
            return new AiProfile
            {
                Selection = Selection ?? new GreedySelection(),
                Risk = Risk ?? new ThresholdRisk(),
                MistakeChance = MistakeChance,
            };
        }
    }
}
