using System.Collections.Generic;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.MatchFlow;
using Zonk.Progress;

namespace Zonk.Table
{
    /// <summary>ID слотов, к которым обращается код. Остальные слоты код не знает и знать не должен.</summary>
    public static class SlotIds
    {
        public const string Cup = "cup";
        public const string DiceSkin = "dice_skin";
        public const string Environment = "environment";
        public const string RollStyle = "roll_style";
    }

    /// <summary>Собирает участников партии из сохранения и конфигов.</summary>
    public sealed class ParticipantFactory
    {
        private readonly ContentDatabase _content;
        private readonly GameConfig _config;
        private readonly ILoadout _loadout;
        private readonly IInventory _inventory;

        public ParticipantFactory(ContentDatabase content, GameConfig config, ILoadout loadout, IInventory inventory)
        {
            _content = content;
            _config = config;
            _loadout = loadout;
            _inventory = inventory;
        }

        public CosmeticSlotConfig Slot(string id) => _content.Get<CosmeticSlotConfig>(id);

        public Color PlayerColor(int index)
        {
            return _config.PlayerColors != null && index < _config.PlayerColors.Length ? _config.PlayerColors[index] : Color.white;
        }

        /// <summary>Местный игрок кампании: его кости, скин и стакан из экипировки.</summary>
        public MatchParticipant LocalPlayer(string name, bool allowSpecial)
        {
            var dice = new List<DieConfig>(_loadout.GetDice());
            if (!allowSpecial)
                ReplaceSpecial(dice);

            return new MatchParticipant
            {
                Name = name,
                Color = PlayerColor(0),
                Controller = ControllerKind.Local,
                Dice = dice,
                DiceSkin = _loadout.GetEquipped(Slot(SlotIds.DiceSkin)),
                Cup = _loadout.GetEquipped(Slot(SlotIds.Cup)),
                RollStyles = OwnedRollStyles(),
            };
        }

        public MatchParticipant HotSeatPlayer(int index, HotSeatPlayerSave save, bool allowSpecial)
        {
            var dice = Loadout.ResolveDice(save.Dice, _content, _config, _inventory, allowSpecial);
            var skin = _content.Get<CosmeticItemConfig>(save.SkinId);
            if (skin == null || !_inventory.IsOwned(skin))
                skin = _loadout.GetEquipped(Slot(SlotIds.DiceSkin));

            return new MatchParticipant
            {
                Name = save.Name,
                Color = PlayerColor(index),
                Controller = ControllerKind.Local,
                Dice = dice,
                DiceSkin = skin,
                Cup = _loadout.GetEquipped(Slot(SlotIds.Cup)),
                RollStyles = OwnedRollStyles(),
            };
        }

        public MatchParticipant Opponent(OpponentConfig opponent, string name)
        {
            var dice = new List<DieConfig>();
            for (var i = 0; i < Core.Match.ZonkMatch.DiceCount; i++)
            {
                var die = i < opponent.Dice.Count ? opponent.Dice[i] : null;
                dice.Add(die != null ? die : _config.StandardDie);
            }

            var cupSlot = Slot(SlotIds.Cup);
            var skinSlot = Slot(SlotIds.DiceSkin);
            return new MatchParticipant
            {
                Name = name,
                Color = opponent.IsBoss ? UiBossColor : PlayerColor(1),
                Controller = ControllerKind.Ai,
                Dice = dice,
                DiceSkin = opponent.DiceSkin != null ? opponent.DiceSkin : skinSlot != null ? skinSlot.DefaultItem : null,
                Cup = opponent.Cup != null ? opponent.Cup : cupSlot != null ? cupSlot.DefaultItem : null,
                Opponent = opponent,
                AiProfile = opponent.Ai != null ? opponent.Ai.ToProfile() : null,
                AiThinkDelay = opponent.Ai != null ? opponent.Ai.ThinkDelay : new Vector2(0.6f, 1.4f),
                RollStyles = opponent.RollStyle != null ? new List<RollStyleConfig> { opponent.RollStyle } : new List<RollStyleConfig>(),
            };
        }

        /// <summary>Отмеченные игроком стили броска (слот с мультивыбором в магазине).</summary>
        private List<RollStyleConfig> OwnedRollStyles()
        {
            var styles = new List<RollStyleConfig>();
            foreach (var item in _loadout.GetEquippedSet(Slot(SlotIds.RollStyle)))
            {
                if (item.Payload is RollStylePayload payload && payload.Style != null)
                    styles.Add(payload.Style);
            }

            return styles;
        }

        public static IReadOnlyList<DieSpec> Specs(IReadOnlyList<DieConfig> dice)
        {
            var specs = new DieSpec[dice.Count];
            for (var i = 0; i < dice.Count; i++)
                specs[i] = dice[i] != null ? dice[i].Spec : DieSpec.Standard;
            return specs;
        }

        private void ReplaceSpecial(List<DieConfig> dice)
        {
            for (var i = 0; i < dice.Count; i++)
            {
                if (dice[i] == null || dice[i].IsSpecial)
                    dice[i] = _config.StandardDie;
            }
        }

        private static readonly Color UiBossColor = new Color(0.95f, 0.45f, 0.35f);
    }
}
