using UnityEngine;

namespace Zonk.Presentation
{
    /// <summary>
    /// Модель слота «Кисть» (перчатка, особая рука; Tools/Blender/zonk_hands.py): меш на скелете руки. HandView привязывает
    /// её кости к костям руки по именам, поэтому пальцы гнутся вместе с рукой. Ставит генератор (ZonkSetup.HandLook).
    /// </summary>
    public sealed class HandLookPart : MonoBehaviour
    {
        [Tooltip("Модель заменяет кисть: рука прячется (робот, скелет, клешня). Выключено — перчатка поверх руки")]
        public bool HidesHand;

        [Tooltip("Первый материал модели — кожа: HandView ставит туда кожу игрока (предплечье, открытые места)")]
        public bool SkinSlot;
    }
}
