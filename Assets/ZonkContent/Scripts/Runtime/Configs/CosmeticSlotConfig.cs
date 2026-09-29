using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>
    /// Слот косметики: стол, стакан, скин костей, локация, лампа… Новая деталь интерьера = новый ассет слота
    /// плюс якорь CosmeticAnchor с тем же ID в сцене и ракурс камеры для магазина. Код не нужен.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Cosmetic Slot", fileName = "Slot")]
    public sealed class CosmeticSlotConfig : ContentConfig
    {
        public Sprite Icon;

        [Tooltip("Порядок вкладки в магазине")]
        public int Order;

        [Tooltip("ID ракурса камеры (CameraShot) при выборе вкладки в магазине")]
        public string CameraShotId = "shop_table";

        [Tooltip("Что надето, пока игрок ничего не выбрал. Обычно бесплатный базовый предмет")]
        public CosmeticItemConfig DefaultItem;

        [Tooltip("Как предмет применяется в сцене. Почти всегда AnchorPrefabApplier")]
        [SerializeReference, SubclassSelector]
        public CosmeticApplier Applier = new AnchorPrefabApplier();

        public bool ShowInShop = true;

        [Tooltip("Можно отметить несколько предметов сразу; при использовании берётся случайный из отмеченных (стиль броска)")]
        public bool MultiSelect;

        [Tooltip("Предмет этого слота задаёт освещение сцены (локации): профиль из PrefabPayload.Lighting")]
        public bool DrivesLighting;
    }
}
