using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Services.Saves
{
    /// <summary>
    /// Сохранение игрока поверх ICloudSaveService. Данные делятся на разделы по ключу,
    /// у каждого раздела свой номер версии. Когда структура раздела меняется, игра добавляет
    /// ISaveMigration, и старые сохранения игроков переводятся в новую структуру при загрузке.
    ///
    /// Разделы, которые текущая версия игры не запрашивала, сохраняются как есть и не теряются.
    /// Загрузку запускает PlatformInitializer до сигнала готовности, поэтому к первому экрану данные уже есть.
    /// </summary>
    public interface ISaveStore
    {
        bool IsLoaded { get; }

        /// <summary>
        /// Облако прочитать не удалось. Запись в облако в этой сессии выключена,
        /// иначе пустые данные затёрли бы прогресс игрока.
        /// </summary>
        bool IsCloudWriteBlocked { get; }

        /// <summary>Читает сохранение из облака. Повторный вызов ничего не делает.</summary>
        UniTask LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>Дождаться окончания загрузки. После неё Get можно вызывать.</summary>
        UniTask WaitLoadedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Раздел сохранения. Возвращает один и тот же объект на ключ: его меняют напрямую,
        /// затем вызывают RequestSave. Если раздела нет, возвращается новый объект.
        /// Тип должен быть [Serializable]-классом для JsonUtility.
        /// </summary>
        T Get<T>(string key) where T : class, new();

        /// <summary>Текущая версия раздела: 1 плюс число миграций для этого ключа.</summary>
        int GetVersion(string key);

        /// <summary>
        /// Сохранить в ближайшие секунды. Частые вызовы склеиваются в одну запись:
        /// у площадок есть лимиты (VK: 1000 вызовов хранилища в час).
        /// </summary>
        void RequestSave();

        /// <summary>Сохранить сразу. Для важных моментов: покупка, конец партии.</summary>
        UniTask SaveNowAsync(CancellationToken cancellationToken = default);
    }
}
