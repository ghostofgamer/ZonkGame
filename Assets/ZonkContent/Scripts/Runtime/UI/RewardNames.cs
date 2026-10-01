using System;
using Zonk.Configs;

namespace Zonk.UI
{
    /// <summary>
    /// Понятное название предмета в награде: у костей и стилей броска одно имя ничего не говорит («Потёртая»,
    /// «Бабушкин»), поэтому к нему добавляется, что это: «кость «Потёртая»», «бросок «Бабушкин»».
    /// Косметика вроде «Золотой стакан» называется понятно сама.
    /// </summary>
    public static class RewardNames
    {
        public static string Describe(ContentConfig item, Func<string, string> localize)
        {
            if (item == null)
                return string.Empty;

            var name = localize(item.NameKey);
            string kindKey = null;
            if (item is DieConfig)
                kindKey = "reward.die";
            else if (item is CosmeticItemConfig cosmetic && cosmetic.Payload is RollStylePayload)
                kindKey = "reward.rollStyle";
            else if (item is CosmeticItemConfig skin && skin.Slot != null && skin.Slot.Id == Table.SlotIds.DiceSkin)
                kindKey = "reward.diceSkin";

            if (kindKey == null)
                return name;

            try
            {
                return string.Format(localize(kindKey), name);
            }
            catch (FormatException)
            {
                return name;
            }
        }
    }
}
