using System;
using System.Collections.Generic;

namespace Base.Core.Localization
{
    /// <summary>
    /// Простая локализация на словаре в памяти.
    /// Языки, которых нет в таблице, откатываются на английский.
    /// </summary>
    public sealed class Localization : ILocalization
    {
        public const string Russian = "ru";
        public const string English = "en";

        /// <summary>Язык для всех кодов, которых нет в таблице.</summary>
        public const string Fallback = English;

        private static readonly string[] SupportedLanguages = { Russian, English };

        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _table;

        public Localization()
        {
            _table = LocalizationTable.Build();
            Language = Fallback;
        }

        public string Language { get; private set; }

        public event Action LanguageChanged;

        public static IReadOnlyList<string> Supported => SupportedLanguages;

        public void SetLanguage(string languageCode)
        {
            var normalized = Normalize(languageCode);
            if (normalized == Language)
                return;

            Language = normalized;
            LanguageChanged?.Invoke();
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_table.TryGetValue(key, out var translations))
            {
                if (translations.TryGetValue(Language, out var text))
                    return text;

                if (translations.TryGetValue(Fallback, out var fallbackText))
                    return fallbackText;
            }

            return key;
        }

        /// <summary>
        /// Приводит код языка к поддерживаемому. Площадки присылают как "ru",
        /// так и варианты вида "ru-RU", поэтому берётся только основная часть.
        /// </summary>
        public static string Normalize(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return Fallback;

            var code = languageCode.Trim().ToLowerInvariant();

            var separator = code.IndexOfAny(new[] { '-', '_' });
            if (separator > 0)
                code = code.Substring(0, separator);

            foreach (var supported in SupportedLanguages)
            {
                if (code == supported)
                    return supported;
            }

            return Fallback;
        }
    }
}
