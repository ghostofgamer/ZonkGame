using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Zonk.Core.Modifiers;

namespace Zonk.UI
{
    /// <summary>
    /// Текст правил партии для игрока, из самих правил (MatchModifier.DescriptionKey и DescriptionArgs): что действует
    /// в партии, то и написано. Строка-значение с «@» — ключ текста (название комбинации, грани). Дробные числа —
    /// с запятой, кроме английского. Для окон (редко), память выделять можно.
    /// </summary>
    public static class RuleTexts
    {
        private static readonly StringBuilder Builder = new StringBuilder();

        /// <summary>Одно правило. Нет описания — пустая строка.</summary>
        public static string Describe(MatchModifier modifier, Func<string, string> localize, string language)
        {
            if (modifier == null || string.IsNullOrEmpty(modifier.DescriptionKey))
                return string.Empty;

            var source = modifier.DescriptionArgs ?? Array.Empty<object>();
            var args = new object[source.Length];
            for (var i = 0; i < source.Length; i++)
                args[i] = Value(source[i], localize, language);

            var format = localize(modifier.DescriptionKey);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>Все правила списком, каждое с новой строки и маркером. Без описаний — пустая строка.</summary>
        public static string List(IReadOnlyList<MatchModifier> modifiers, Func<string, string> localize, string language,
            string bullet = "• ")
        {
            Builder.Clear();
            if (modifiers == null)
                return string.Empty;

            for (var i = 0; i < modifiers.Count; i++)
            {
                var text = Describe(modifiers[i], localize, language);
                if (text.Length == 0)
                    continue;

                if (Builder.Length > 0)
                    Builder.Append('\n');
                Builder.Append(bullet).Append(text);
            }

            return Builder.ToString();
        }

        /// <summary>Есть ли у правил хоть одно описание.</summary>
        public static bool HasAny(IReadOnlyList<MatchModifier> modifiers)
        {
            if (modifiers == null)
                return false;

            foreach (var modifier in modifiers)
            {
                if (modifier != null && !string.IsNullOrEmpty(modifier.DescriptionKey))
                    return true;
            }

            return false;
        }

        private static object Value(object value, Func<string, string> localize, string language)
        {
            switch (value)
            {
                case string text when text.StartsWith("@", StringComparison.Ordinal):
                    return localize(text.Substring(1));
                case float number:
                    return Number(number, language);
                case double number:
                    return Number(number, language);
                default:
                    return value;
            }
        }

        /// <summary>2 → «2», 1.5 → «1,5» (в английском «1.5»).</summary>
        private static string Number(double value, string language)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.001)
                return ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

            var text = value.ToString("0.##", CultureInfo.InvariantCulture);
            return language == "en" ? text : text.Replace('.', ',');
        }
    }
}
