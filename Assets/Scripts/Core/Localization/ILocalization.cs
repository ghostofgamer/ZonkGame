using System;

namespace Base.Core.Localization
{
    /// <summary>
    /// Язык интерфейса. Определяется автоматически по языку из SDK площадки,
    /// это требование модерации Яндекс Игр (п. 2.14).
    /// </summary>
    public interface ILocalization
    {
        /// <summary>Текущий язык в формате ISO 639-1, например "ru" или "en".</summary>
        string Language { get; }

        /// <summary>Язык сменился: подписчики должны перестроить тексты.</summary>
        event Action LanguageChanged;

        /// <summary>
        /// Устанавливает язык по коду от площадки. Код может быть любым,
        /// неподдерживаемые языки заменяются языком по умолчанию.
        /// </summary>
        void SetLanguage(string languageCode);

        /// <summary>Текст по ключу. Для неизвестного ключа возвращает сам ключ.</summary>
        string Get(string key);
    }
}
