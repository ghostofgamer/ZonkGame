using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>События партии, на которые реагирует соперник.</summary>
    public enum MatchEventType
    {
        MatchStarted,
        SelfZonk,
        OtherZonk,
        SelfHotDice,
        SelfBigBank,
        OtherBigBank,
        SelfRiskyRoll,
        Won,
        Lost,
        PhraseReceived,
        Thinking,

        // Новые события добавляются только в конец: в ассетах хранится номер.

        /// <summary>Соперник отложил дорогую комбинацию (от GameConfig.BigKeepScore).</summary>
        SelfBigKeep,
        OtherBigKeep,

        /// <summary>У соперника горячие кости.</summary>
        OtherHotDice,
    }

    /// <summary>Жесты аватара. Заглушка играет их процедурно, позже аниматор по тем же именам.</summary>
    public enum AvatarGesture
    {
        None,
        Think,
        Cheer,
        Angry,
        SlamTable,
        Laugh,
        Shrug,
        Nod,

        /// <summary>Два удара кулаком подряд: злость боссов. Новые жесты — только в конец.</summary>
        SlamTwice,
    }

    [Serializable]
    public sealed class ReactionEntry
    {
        public MatchEventType Event;
        public AvatarGesture Gesture;

        [Tooltip("Ключи реплик, выбирается случайная. Пусто = без реплики")]
        public List<string> LineKeys = new List<string>();

        [Range(0f, 1f)]
        public float Chance = 1f;
    }

    /// <summary>Набор реакций: событие → жест, реплика. Один набор можно дать многим соперникам.</summary>
    [CreateAssetMenu(menuName = "Zonk/Reaction Set", fileName = "Reactions")]
    public sealed class ReactionSetConfig : ContentConfig
    {
        public List<ReactionEntry> Entries = new List<ReactionEntry>();
    }
}
