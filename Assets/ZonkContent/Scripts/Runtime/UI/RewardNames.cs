using System;
using System.Collections.Generic;
using System.Text;
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
        private static readonly StringBuilder Builder = new StringBuilder();

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

        /// <summary>
        /// Награды одной строкой: «90 монет, кость «Пятёрочная»». ownedMark (необязательно) дописывается к предмету,
        /// который у игрока уже есть (isOwned). Для окон (редко): память выделять можно.
        /// </summary>
        public static string List(IReadOnlyList<Reward> rewards, Func<string, string> localize,
            Func<ContentConfig, bool> isOwned = null, string ownedMark = null)
        {
            if (rewards == null || rewards.Count == 0)
                return string.Empty;

            Builder.Clear();
            foreach (var reward in rewards)
            {
                var start = Builder.Length;
                if (start > 0)
                    Builder.Append(", ");

                switch (reward)
                {
                    case CurrencyReward currency when currency.Currency != null && currency.Amount > 0:
                        Builder.Append(currency.Amount).Append(' ').Append(localize(currency.Currency.NameKey));
                        break;
                    case ContentReward content when content.Item != null:
                        Builder.Append(Describe(content.Item, localize));
                        if (ownedMark != null && isOwned != null && isOwned(content.Item))
                            Builder.Append(' ').Append(ownedMark);
                        break;
                    default:
                        Builder.Length = start;
                        break;
                }
            }

            return Builder.ToString();
        }

        private static readonly string[] Roman = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        /// <summary>Название достижения со ступенью: «Победитель III».</summary>
        public static string AchievementTitle(AchievementConfig achievement, Func<string, string> localize)
        {
            if (achievement == null)
                return string.Empty;

            var name = localize(achievement.NameKey);
            var tier = achievement.Tier;
            return tier > 0 ? name + " " + (tier < Roman.Length ? Roman[tier] : tier.ToString()) : name;
        }

        /// <summary>Описание достижения: «Победы против соперников: 100».</summary>
        public static string AchievementDescription(AchievementConfig achievement, Func<string, string> localize)
        {
            if (achievement == null || string.IsNullOrEmpty(achievement.DescriptionKey))
                return string.Empty;

            try
            {
                return string.Format(localize(achievement.DescriptionKey), achievement.Target);
            }
            catch (FormatException)
            {
                return localize(achievement.DescriptionKey);
            }
        }

        /// <summary>Уже выданные награды одной строкой: «+50 монет, кость «Пятёрочная»». Для сообщений (редко).</summary>
        public static string Granted(IReadOnlyList<Progress.GrantedReward> rewards, Func<string, string> localize)
        {
            if (rewards == null || rewards.Count == 0)
                return string.Empty;

            Builder.Clear();
            foreach (var reward in rewards)
            {
                if (reward.Currency == null && reward.Item == null)
                    continue;

                if (Builder.Length > 0)
                    Builder.Append(", ");
                if (reward.Currency != null)
                    Builder.Append('+').Append(reward.Amount).Append(' ').Append(localize(reward.Currency.NameKey));
                else
                    Builder.Append(Describe(reward.Item, localize));
            }

            return Builder.ToString();
        }
    }
}
