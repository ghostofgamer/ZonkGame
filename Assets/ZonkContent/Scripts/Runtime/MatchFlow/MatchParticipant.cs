using System.Collections.Generic;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Ai;

namespace Zonk.MatchFlow
{
    public enum ControllerKind
    {
        /// <summary>Человек за этим экраном.</summary>
        Local,
        /// <summary>ИИ по профилю.</summary>
        Ai,
        /// <summary>Игрок по сети (будущий онлайн).</summary>
        Remote,
    }

    /// <summary>Участник партии со стороны визуала и управления: кто он, чем играет, как выглядит.</summary>
    public sealed class MatchParticipant
    {
        public string Name;
        public Color Color = Color.white;
        public ControllerKind Controller;

        /// <summary>Кости по слотам 0..5.</summary>
        public IReadOnlyList<DieConfig> Dice;

        public CosmeticItemConfig DiceSkin;
        public CosmeticItemConfig Cup;

        /// <summary>Для ИИ: соперник кампании (аватар, реакции). null у людей.</summary>
        public OpponentConfig Opponent;

        public AiProfile AiProfile;

        /// <summary>Как участник трясёт стакан: на каждый бросок случайный из списка. Пусто = стиль по умолчанию.</summary>
        public List<RollStyleConfig> RollStyles = new List<RollStyleConfig>();
        public Vector2 AiThinkDelay = new Vector2(0.6f, 1.4f);
    }
}
