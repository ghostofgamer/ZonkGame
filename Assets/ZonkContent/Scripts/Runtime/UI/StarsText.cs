using System;
using System.Text;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.UI
{
    /// <summary>
    /// Звёзды за соперника текстом: полученные — золотые, остальные — серые (один символ ★, разный цвет).
    /// Текст условия звезды: ключ условия с его числом.
    /// </summary>
    public static class StarsText
    {
        private static readonly StringBuilder Builder = new StringBuilder();

        /// <summary>Звезда в тексте: значок из UiConfig.Icons, красится текущим цветом текста.</summary>
        public const string Star = "<sprite name=\"star\" tint=1>";

        /// <summary>
        /// Подготовить надпись к звёздам: символа ★ нет в шрифте, его рисует значок из UiConfig.Icons (картинка
        /// белая, красится цветом текста). Вызывать для каждой надписи, где могут быть звёзды.
        /// </summary>
        public static void Prepare(TMPro.TMP_Text label, UiConfig ui)
        {
            if (label == null || ui == null || ui.Icons == null)
                return;

            label.spriteAsset = ui.Icons;
            label.tintAllSprites = true;
        }

        /// <summary>Сколько звёзд можно получить за соперника: победа плюс условия.</summary>
        public static int MaxStars(OpponentConfig opponent)
        {
            return opponent != null ? 1 + opponent.StarConditions.Count : 1;
        }

        public static int CountBits(int mask)
        {
            var count = 0;
            for (; mask != 0; mask &= mask - 1)
                count++;
            return count;
        }

        public static string Render(int mask, int count)
        {
            var gold = ColorUtility.ToHtmlStringRGB(UiColors.Gold);
            Builder.Clear();
            for (var i = 0; i < count; i++)
            {
                var got = (mask & (1 << i)) != 0;
                Builder.Append("<color=#").Append(got ? gold : "9A948C").Append(">").Append(Star).Append("</color>");
            }

            return Builder.ToString();
        }

        public static string Condition(StarCondition condition, Func<string, string> localize)
        {
            if (condition == null)
                return string.Empty;

            var format = localize(condition.TextKey);
            try
            {
                return string.Format(format, condition.TextValue);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
