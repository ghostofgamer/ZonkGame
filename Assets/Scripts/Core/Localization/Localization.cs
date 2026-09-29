using System;
using System.Collections.Generic;

namespace Base.Core.Localization
{
    /// <summary>
    /// Простая локализация на словаре в памяти: встроенная таблица шаблона плюс источники игры.
    /// Список языков берётся из таблиц: столбец языка в CSV игры добавляет язык. Языки, которых нет,
    /// откатываются на английский.
    /// </summary>
    public sealed class Localization : ILocalization
    {
        public const string Russian = "ru";
        public const string English = "en";

        /// <summary>Язык для всех кодов, которых нет в таблице.</summary>
        public const string Fallback = English;

        private static readonly string[] DefaultLanguages = { Russian, English };

        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _table =
            new Dictionary<string, IReadOnlyDictionary<string, string>>();

        private readonly List<string> _languages = new List<string>(DefaultLanguages);

        /// <summary>
        /// sources: тексты игры (обычно CSV из Google Таблицы). Их ключи перекрывают встроенную таблицу шаблона.
        /// Zenject передаёт сюда все биндинги ILocalizationSource, пустой список тоже годится.
        /// </summary>
        public Localization(List<ILocalizationSource> sources)
        {
            Merge(LocalizationTable.Build());

            if (sources != null)
            {
                foreach (var source in sources)
                    Merge(source.Load());
            }

            Language = Fallback;
        }

        public string Language { get; private set; }

        public IReadOnlyList<string> Languages => _languages;

        public event Action LanguageChanged;

        /// <summary>Языки встроенной таблицы шаблона. Языки игры: свойство Languages.</summary>
        public static IReadOnlyList<string> Supported => DefaultLanguages;

        /// <summary>Есть ли текст с таким ключом. Для проверки контента в редакторе.</summary>
        public bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && _table.ContainsKey(key);
        }

        public void SetLanguage(string languageCode)
        {
            var normalized = Normalize(languageCode, _languages);
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

        /// <summary>Приводит код языка к одному из языков шаблона (ru, en).</summary>
        public static string Normalize(string languageCode)
        {
            return Normalize(languageCode, DefaultLanguages);
        }

        /// <summary>
        /// Приводит код языка к поддерживаемому. Площадки присылают как "ru",
        /// так и варианты вида "ru-RU", поэтому берётся только основная часть.
        /// </summary>
        public static string Normalize(string languageCode, IReadOnlyList<string> supported)
        {
            if (string.IsNullOrEmpty(languageCode))
                return Fallback;

            var code = languageCode.Trim().ToLowerInvariant();

            var separator = code.IndexOfAny(new[] { '-', '_' });
            if (separator > 0)
                code = code.Substring(0, separator);

            foreach (var language in supported)
            {
                if (code == language)
                    return language;
            }

            return Fallback;
        }

        private void Merge(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> table)
        {
            foreach (var entry in table)
            {
                _table[entry.Key] = entry.Value;
                foreach (var language in entry.Value.Keys)
                {
                    if (!_languages.Contains(language))
                        _languages.Add(language);
                }
            }
        }
    }
}
