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
        public const string Quests = "zonk_quests";
        public const string Mastery = "zonk_mastery";
        public const string AdBonus = "zonk_ad_bonus";
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

        /// <summary>Наборы, чьи разовые награды (монеты, энергия) уже выданы.</summary>
        public List<string> RewardedThemes = new List<string>();
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

        /// <summary>Сохранённые наборы костей (ID по слотам). Dice — копия активного набора.</summary>
        public List<DicePresetSave> Presets = new List<DicePresetSave>();

        public int ActivePreset;
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

    [Serializable]
    public sealed class DicePresetSave
    {
        public List<string> Dice = new List<string>();
    }

    [Serializable]
    public sealed class QuestEntry
    {
        public string Id;
        public int Progress;
        public bool Claimed;
    }

    [Serializable]
    public sealed class QuestSave
    {
        /// <summary>Номер дня и недели выданных заданий (QuestCalendar). -1: ещё не выдавались.</summary>
        public int Day = -1;
        public int Week = -1;

        public List<QuestEntry> Daily = new List<QuestEntry>();
        public List<QuestEntry> Weekly = new List<QuestEntry>();

        /// <summary>Сколько дневных заданий заменено сегодня.</summary>
        public int Rerolls;

        /// <summary>Последний день, когда игрок заходил (для недельного «заходи в разные дни»).</summary>
        public int LastVisitDay = -1;
    }

    [Serializable]
    public sealed class MasteryEntry
    {
        public string Id;
        public int Points;

        /// <summary>До какого уровня награды уже выданы.</summary>
        public int RewardedLevel;
    }

    [Serializable]
    public sealed class MasterySave
    {
        public List<MasteryEntry> Dice = new List<MasteryEntry>();
    }

    /// <summary>Награды за рекламу в главном меню: сколько раз взята каждая сегодня.</summary>
    [Serializable]
    public sealed class AdBonusSave
    {
        public int Day = -1;

        /// <summary>Прежнее поле (одна кнопка монет), не используется.</summary>
        public int Count;

        public List<AdBonusEntry> Offers = new List<AdBonusEntry>();
    }

    [Serializable]
    public sealed class AdBonusEntry
    {
        public string Id;
        public int Count;
    }
}
