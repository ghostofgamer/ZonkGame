using System;
using System.Collections.Generic;

namespace Zonk.Progress
{
    /// <summary>
    /// Разделы сохранения игры (ISaveStore). Везде только ID и числа: новый контент не требует миграций.
    /// Переименовать или перенести поле можно только вместе с новой ISaveMigration.
    /// </summary>
    public static class SaveKeys
    {
        public const string Wallet = "zonk_wallet";
        public const string Inventory = "zonk_inventory";
        public const string Loadout = "zonk_loadout";
        public const string Campaign = "zonk_campaign";
        public const string Settings = "zonk_settings";
        public const string HotSeat = "zonk_hotseat";
    }

    [Serializable]
    public sealed class CurrencyEntry
    {
        public string Id;
        public int Amount;

        /// <summary>Момент последнего начисления восстановления, секунды Unix (UTC).</summary>
        public long RegenUnix;
    }

    [Serializable]
    public sealed class WalletSave
    {
        public List<CurrencyEntry> Currencies = new List<CurrencyEntry>();
    }

    [Serializable]
    public sealed class AdProgressEntry
    {
        public string Id;
        public int Watched;
    }

    [Serializable]
    public sealed class InventorySave
    {
        public List<string> Owned = new List<string>();
        public List<AdProgressEntry> AdProgress = new List<AdProgressEntry>();
    }

    [Serializable]
    public sealed class EquippedEntry
    {
        public string SlotId;
        public string ItemId;
    }

    /// <summary>Отмеченные предметы слота с мультивыбором (например, стили броска).</summary>
    [Serializable]
    public sealed class MultiEquippedEntry
    {
        public string SlotId;
        public List<string> Items = new List<string>();
    }

    [Serializable]
    public sealed class LoadoutSave
    {
        public List<EquippedEntry> Equipped = new List<EquippedEntry>();

        /// <summary>Слоты с мультивыбором: несколько отмеченных предметов.</summary>
        public List<MultiEquippedEntry> MultiEquipped = new List<MultiEquippedEntry>();

        /// <summary>ID костей по слотам 0..5. Пусто или неизвестный ID = обычная кость.</summary>
        public List<string> Dice = new List<string>();
    }

    [Serializable]
    public sealed class CampaignSave
    {
        public List<string> Beaten = new List<string>();
        public List<string> SeenIntros = new List<string>();
        public List<string> SeenOutros = new List<string>();
    }

    [Serializable]
    public sealed class SettingsSave
    {
        public bool Sound = true;
        public bool Music = true;

        /// <summary>Скорость анимаций: 1 = обычная, 2 = быстрая.</summary>
        public int Speed = 1;
    }

    [Serializable]
    public sealed class HotSeatPlayerSave
    {
        public string Name;
        public List<string> Dice = new List<string>();
        public string SkinId;
    }

    public enum FirstPlayerMode
    {
        Random = 0,
        PlayerOne = 1,
        Alternate = 2,
    }

    [Serializable]
    public sealed class HotSeatSave
    {
        public int Target;
        public FirstPlayerMode FirstPlayer = FirstPlayerMode.Random;
        public bool SpecialDice = true;

        /// <summary>Кто ходил первым в прошлой партии, для режима «по очереди».</summary>
        public int LastFirstPlayer = 1;

        public List<HotSeatPlayerSave> Players = new List<HotSeatPlayerSave>();
    }
}
