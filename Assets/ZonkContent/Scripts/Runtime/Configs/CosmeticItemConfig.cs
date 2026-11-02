using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>
    /// Предмет косметики. Payload решает, что меняется: целый префаб (новая модель), только материал
    /// (текстура на базовой модели слота) или меш и материал (для костей).
    /// </summary>
    /// <summary>
    /// Редкость предмета: цвет рамки карточки и шанс выпасть из сундука. Значения записаны в ассетах числом, поэтому
    /// новые — только в конец; порядок «обычный → легендарный» — RarityOrder.Rank, а не число.
    /// </summary>
    public enum Rarity
    {
        Common,
        Rare,
        Legendary,
        Epic,
    }

    /// <summary>Ранг редкости для сортировки и сравнения: обычный 0, редкий 1, эпический 2, легендарный 3.</summary>
    public static class RarityOrder
    {
        public const int Count = 4;

        public static int Rank(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare: return 1;
                case Rarity.Epic: return 2;
                case Rarity.Legendary: return 3;
                default: return 0;
            }
        }
    }

    [CreateAssetMenu(menuName = "Zonk/Cosmetic Item", fileName = "Item")]
    public sealed class CosmeticItemConfig : ContentConfig
    {
        public CosmeticSlotConfig Slot;
        public Sprite Icon;
        public int Order;

        [Tooltip("Редкость: цвет в интерфейсе, шанс в сундуке")]
        public Rarity Rarity;

        [SerializeReference, SubclassSelector]
        public CosmeticPayload Payload = new PrefabPayload();

        public Price Price = new Price();

        [Tooltip("Не показывать в магазине (например, предметы боссов)")]
        public bool HiddenInShop;

        [Tooltip("Вкладка магазина, если не своего слота: слот без вкладки продаёт вещи в чужой " +
                 "(фантазийная кожа руки — во вкладке «Рукав»). Пусто — вкладка своего слота")]
        public CosmeticSlotConfig ShopTab;

        /// <summary>Слот, во вкладке которого предмет лежит в магазине.</summary>
        public CosmeticSlotConfig TabSlot => ShopTab != null ? ShopTab : Slot;
    }
}
