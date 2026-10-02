using UnityEditor;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Места безделушек (волна 2, 02.10.2026): у слота decor 3 дополнительных места (CosmeticSlotConfig.ExtraSpots),
    /// открываются талантом «Хозяин стола» (TalentEffect.DecorSpotsExtra). Якоря и ракурсы мест — ZonkSetup.Scenes
    /// (DecorSpots, shop_decor_2..4). Число ставится, пока у слота 0: ручная правка остаётся.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const int DecorExtraSpots = 3;

        static partial void BuildDecorSpots(GameConfig config)
        {
            var slot = SlotAt(DecorSlotId);
            if (slot == null || slot.ExtraSpots > 0)
                return;

            slot.ExtraSpots = DecorExtraSpots;
            EditorUtility.SetDirty(slot);
        }
    }
}
