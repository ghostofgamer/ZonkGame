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
        public const string Avatar = "avatar";
        public const string Frame = "frame";
    }

    /// <summary>Собирает участников партии из сохранения и конфигов.</summary>
    public sealed class ParticipantFactory
    {
        private readonly ContentDatabase _content;
        private readonly GameConfig _config;
        private readonly ILoadout _loadout;
        private readonly IInventory _inventory;
        private readonly IDieMastery _mastery;

        public ParticipantFactory(ContentDatabase content, GameConfig config, ILoadout loadout, IInventory inventory,
            IDieMastery mastery)
        {
            _mastery = mastery;
            _content = content;
            _config = config;
            _loadout = loadout;
            _inventory = inventory;
        }

        /// <summary>Уровни мастерства костей игрока по слотам: вид кости на столе.</summary>
        public List<int> MasteryLevels(IReadOnlyList<DieConfig> dice)
        {
            var result = new List<int>(dice.Count);
            foreach (var die in dice)
                result.Add(_mastery.GetLevel(die));
            return result;
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
                MasteryLevels = MasteryLevels(dice),
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
                MasteryLevels = MasteryLevels(dice),
            };
        }

        public MatchParticipant Opponent(OpponentConfig opponent, string name)
        {
            var dice = new List<DieConfig>();
            for (var i = 0; i < Core.Match.ZonkMatch.DiceCount; i++)
            {
                var die = i < opponent.Dice.Count ? opponent.Dice[i] : null;
                // Правило то же, что у игрока: особая кость — только в одном слоте.
                if (die == null || (die.IsSpecial && dice.Contains(die)))
                    die = _config.StandardDie;
                dice.Add(die);
            }

            var cupSlot = Slot(SlotIds.Cup);
            var skinSlot = Slot(SlotIds.DiceSkin);
            return new MatchParticipant
            {
                Name = name,
                Color = opponent.IsBoss ? _config.BossColor : PlayerColor(1),
                Controller = ControllerKind.Ai,
                Dice = dice,
                DiceSkin = opponent.DiceSkin != null ? opponent.DiceSkin : skinSlot != null ? skinSlot.DefaultItem : null,
                Cup = opponent.Cup != null ? opponent.Cup : cupSlot != null ? cupSlot.DefaultItem : null,
                Opponent = opponent,
                AiProfile = opponent.Ai != null ? opponent.Ai.ToProfile() : null,
                AiThinkDelay = opponent.Ai != null ? opponent.Ai.ThinkDelay : _config.DefaultAiThinkDelay,
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
    }
}
