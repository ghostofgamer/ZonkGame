using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>Глава кампании: локация, обычные соперники по порядку, последний — босс главы; грозные версии боссов.</summary>
    [CreateAssetMenu(menuName = "Zonk/Chapter", fileName = "Chapter")]
    public sealed class ChapterConfig : ContentConfig
    {
        public int Order;

        [Tooltip("Локация главы: на время партий ставится вместо выбранной игроком")]
        public CosmeticItemConfig Environment;

        public List<StoryLine> Intro = new List<StoryLine>();
        public List<StoryLine> Outro = new List<StoryLine>();

        public List<OpponentConfig> Opponents = new List<OpponentConfig>();

        [Tooltip("Грозные версии боссов главы (OpponentConfig.DreadOf). Видны в главе после её соперников, открываются " +
                 "звёздами у обычного босса. На открытие глав и «текущую главу» не влияют")]
        public List<OpponentConfig> DreadBosses = new List<OpponentConfig>();
    }
}
