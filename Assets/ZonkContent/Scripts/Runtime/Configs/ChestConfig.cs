using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>Строка таблицы сундука: монеты или вещь редкости Rarity; вес — шанс относительно остальных строк.</summary>
    [Serializable]
    public sealed class ChestDrop
    {
        [Tooltip("Вещь (иначе монеты)")]
        public bool Item;

        public Rarity Rarity;

        [Min(0f)] public float Weight = 1f;

        [Tooltip("Монеты от и до. У вещи — замена, если все вещи этой редкости уже открыты")]
        public int MinCoins = 50;

        public int MaxCoins = 150;
    }

    /// <summary>
    /// Сундук за победы: каждые WinsPerChest побед против соперников — сундук (копятся до MaxStored). Внутри — по таблице
    /// Drops: чаще монеты, иногда вещь из Items (только ещё не открытая; открыто всё — монеты). Счётчик невезения:
    /// LegendaryPity сундуков подряд без легендарки — следующий её даёт. Сундук только за игру, не за деньги.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Chest", fileName = "Chest")]
    public sealed class ChestConfig : ScriptableObject
    {
        [Min(1)] public int WinsPerChest = 3;

        [Tooltip("Сколько сундуков можно копить, не открывая")]
        [Min(1)] public int MaxStored = 10;

        [Tooltip("Гарантированная легендарка после стольких сундуков подряд без неё (0 — без гарантии)")]
        [Min(0)] public int LegendaryPity = 150;

        public CurrencyConfig Coins;

        [Tooltip("Картинка сундука в меню и окне открытия")]
        public Sprite Icon;

        public List<ChestDrop> Drops = new List<ChestDrop>();

        [Tooltip("Вещи, которые могут выпасть")]
        public List<CosmeticItemConfig> Items = new List<CosmeticItemConfig>();
    }
}
