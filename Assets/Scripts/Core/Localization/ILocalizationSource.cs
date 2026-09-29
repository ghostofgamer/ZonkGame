using System.Collections.Generic;

namespace Base.Core.Localization
{
    /// <summary>
    /// Тексты игры: ключ, затем перевод на каждый язык. Игра биндит свои источники в инсталлере
    /// (Container.Bind&lt;ILocalizationSource&gt;()...), Localization собирает их при создании.
    /// Ключи источников перекрывают встроенную LocalizationTable шаблона.
    /// Обычный источник: CSV из Google Таблицы, см. CsvLocalizationParser.
    /// </summary>
    public interface ILocalizationSource
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load();
    }
}
