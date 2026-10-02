using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;
using Zonk.Core.Match;

namespace Zonk.Progress
{
    /// <summary>Что надето: предмет в каждом слоте косметики и шесть костей игрока.</summary>
    public interface ILoadout
    {
        /// <summary>Надетый предмет слота. У слота с мультивыбором — первый отмеченный.</summary>
        CosmeticItemConfig GetEquipped(CosmeticSlotConfig slot);

        /// <summary>Все отмеченные предметы слота с мультивыбором (минимум один: базовый предмет слота).</summary>
        IReadOnlyList<CosmeticItemConfig> GetEquippedSet(CosmeticSlotConfig slot);

        bool IsEquipped(CosmeticItemConfig item);

        /// <summary>Отметить или снять предмет слота с мультивыбором. Последний отмеченный не снимается.</summary>
        void Toggle(CosmeticItemConfig item);

        /// <summary>Надеть предмет. В слоте с мультивыбором — добавить к отмеченным.</summary>
        void Equip(CosmeticItemConfig item);

        /// <summary>Шесть костей игрока (для кампании). Неизвестные ID заменяются обычной костью.</summary>
        IReadOnlyList<DieConfig> GetDice();

        void SetDie(int slot, DieConfig die);

        /// <summary>Сколько наборов костей можно сохранить (GameConfig.DicePresetCount).</summary>
        int PresetCount { get; }

        /// <summary>Активный набор: смена костей сразу сохраняется в него.</summary>
        int ActivePreset { get; }

        /// <summary>Переключиться на набор. Пустой набор начинается с текущих костей.</summary>
        void SelectPreset(int index);

        event Action Changed;
    }

    public sealed class Loadout : ILoadout
    {
        private readonly ISaveStore _saves;
        private readonly ContentDatabase _content;
        private readonly GameConfig _config;
        private readonly IInventory _inventory;

        public Loadout(ISaveStore saves, ContentDatabase content, GameConfig config, IInventory inventory)
        {
            _saves = saves;
            _content = content;
            _config = config;
            _inventory = inventory;
        }

        public event Action Changed;

        private LoadoutSave Data => _saves.Get<LoadoutSave>(SaveKeys.Loadout);

        public CosmeticItemConfig GetEquipped(CosmeticSlotConfig slot)
        {
            if (slot != null && slot.MultiSelect)
            {
                FillEquippedSet(slot, _setBuffer);
                return _setBuffer.Count > 0 ? _setBuffer[0] : null;
            }

            if (slot == null)
                return null;

            foreach (var entry in Data.Equipped)
            {
                if (entry.SlotId != slot.Id)
                    continue;

                var item = _content.Get<CosmeticItemConfig>(entry.ItemId);
                if (item != null && item.Slot == slot && _inventory.IsOwned(item))
                    return item;
            }

            return slot.DefaultItem;
        }

        public void Equip(CosmeticItemConfig item)
        {
            if (item == null || item.Slot == null || !_inventory.IsOwned(item))
                return;

            if (item.Slot.MultiSelect)
            {
                if (!IsEquipped(item))
                    Toggle(item);
                return;
            }

            var data = Data;
            var found = false;
            foreach (var entry in data.Equipped)
            {
                if (entry.SlotId == item.Slot.Id)
                {
                    entry.ItemId = item.Id;
                    found = true;
                }
            }

            if (!found)
                data.Equipped.Add(new EquippedEntry { SlotId = item.Slot.Id, ItemId = item.Id });

            _saves.RequestSave();
            Changed?.Invoke();
        }

        public IReadOnlyList<CosmeticItemConfig> GetEquippedSet(CosmeticSlotConfig slot)
        {
            var result = new List<CosmeticItemConfig>();
            FillEquippedSet(slot, result);
            return result;
        }

        // Рабочий список для GetEquipped, IsEquipped и Toggle: их зовут при каждой отрисовке магазина и сборке участника.
        private readonly List<CosmeticItemConfig> _setBuffer = new List<CosmeticItemConfig>();

        private void FillEquippedSet(CosmeticSlotConfig slot, List<CosmeticItemConfig> result)
        {
            result.Clear();
            if (slot == null)
                return;

            var entry = MultiEntry(slot, false);
            if (entry != null)
            {
                foreach (var id in entry.Items)
                {
                    var item = _content.Get<CosmeticItemConfig>(id);
                    if (item != null && item.Slot == slot && _inventory.IsOwned(item) && !result.Contains(item))
                        result.Add(item);
                }
            }

            if (result.Count == 0 && slot.DefaultItem != null)
                result.Add(slot.DefaultItem);
        }

        public bool IsEquipped(CosmeticItemConfig item)
        {
            if (item == null || item.Slot == null)
                return false;

            if (!item.Slot.MultiSelect)
                return GetEquipped(item.Slot) == item;

            FillEquippedSet(item.Slot, _setBuffer);
            return _setBuffer.Contains(item);
        }

        public void Toggle(CosmeticItemConfig item)
        {
            if (item == null || item.Slot == null || !item.Slot.MultiSelect || !_inventory.IsOwned(item))
                return;

            // Хранимый набор начинается с фактического: если он пуст, в нём базовый предмет слота.
            FillEquippedSet(item.Slot, _setBuffer);
            var entry = MultiEntry(item.Slot, true);
            entry.Items.Clear();
            foreach (var equipped in _setBuffer)
                entry.Items.Add(equipped.Id);

            if (entry.Items.Contains(item.Id))
            {
                if (entry.Items.Count <= 1)
                    return;
                entry.Items.Remove(item.Id);
            }
            else
            {
                entry.Items.Add(item.Id);
            }

            _saves.RequestSave();
            Changed?.Invoke();
        }

        private MultiEquippedEntry MultiEntry(CosmeticSlotConfig slot, bool create)
        {
            var data = Data;
            foreach (var entry in data.MultiEquipped)
            {
                if (entry.SlotId == slot.Id)
                    return entry;
            }

            if (!create)
                return null;

            var created = new MultiEquippedEntry { SlotId = slot.Id };
            data.MultiEquipped.Add(created);
            return created;
        }

        public IReadOnlyList<DieConfig> GetDice()
        {
            return ResolveDice(Data.Dice, _content, _config, _inventory);
        }

        public void SetDie(int slot, DieConfig die)
        {
            if (slot < 0 || slot >= ZonkMatch.DiceCount || die == null)
                return;

            var data = Data;
            while (data.Dice.Count < ZonkMatch.DiceCount)
                data.Dice.Add(_config.StandardDie != null ? _config.StandardDie.Id : string.Empty);

            data.Dice[slot] = die.Id;
            EnsurePresets(data);
            data.Presets[ActiveIndex(data)].Dice = new List<string>(data.Dice);
            _saves.RequestSave();
            Changed?.Invoke();
        }

        public int PresetCount => Math.Max(1, _config.DicePresetCount);

        public int ActivePreset => ActiveIndex(Data);

        public void SelectPreset(int index)
        {
            if (index < 0 || index >= PresetCount)
                return;

            var data = Data;
            EnsurePresets(data);
            var active = ActiveIndex(data);
            if (index == active)
                return;

            data.Presets[active].Dice = new List<string>(data.Dice);
            var target = data.Presets[index];
            if (target.Dice.Count == 0)
                target.Dice = new List<string>(data.Dice);

            data.ActivePreset = index;
            data.Dice = new List<string>(target.Dice);
            _saves.RequestSave();
            Changed?.Invoke();
        }

        private int ActiveIndex(LoadoutSave data)
        {
            return Math.Max(0, Math.Min(data.ActivePreset, PresetCount - 1));
        }

        /// <summary>Наборов не меньше PresetCount; первый заполняется текущими костями (старые сохранения без наборов).</summary>
        private void EnsurePresets(LoadoutSave data)
        {
            while (data.Presets.Count < PresetCount)
                data.Presets.Add(new DicePresetSave());

            var active = data.Presets[ActiveIndex(data)];
            if (active.Dice.Count == 0 && data.Dice.Count > 0)
                active.Dice = new List<string>(data.Dice);
        }

        /// <summary>
        /// ID костей → конфиги. Неизвестные, не открытые, повторы особых и сверх лимита особых заменяются обычной костью.
        /// </summary>
        public static List<DieConfig> ResolveDice(IReadOnlyList<string> ids, ContentDatabase content, GameConfig config,
            IInventory inventory, bool allowSpecial = true)
        {
            var result = new List<DieConfig>(ZonkMatch.DiceCount);
            var special = 0;
            for (var i = 0; i < ZonkMatch.DiceCount; i++)
            {
                var id = ids != null && i < ids.Count ? ids[i] : null;
                var die = content.Get<DieConfig>(id);

                if (die == null || !inventory.IsOwned(die) ||
                    (die.IsSpecial && (!allowSpecial || special >= config.MaxSpecialDice || result.Contains(die))))
                {
                    die = config.StandardDie;
                }

                // Особая кость занимает только один слот (повтор выше заменён обычной): одинаковые особые не складывают перекос.
                if (die != null && die.IsSpecial)
                    special++;

                result.Add(die);
            }

            return result;
        }
    }
}
