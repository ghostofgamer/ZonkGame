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
    [CreateAssetMenu(menuName = "Zonk/Cosmetic Item", fileName = "Item")]
    public sealed class CosmeticItemConfig : ContentConfig
    {
        public CosmeticSlotConfig Slot;
        public Sprite Icon;
        public int Order;

        [SerializeReference, SubclassSelector]
        public CosmeticPayload Payload = new PrefabPayload();

        public Price Price = new Price();

        [Tooltip("Не показывать в магазине (например, предметы боссов)")]
        public bool HiddenInShop;
    }
}
