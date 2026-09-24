using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Platform
{
    /// <summary>
    /// Жизненный цикл площадки и данные игрока.
    /// Инициализация SDK, сигналы "игра готова" и "геймплей идёт", авторизация.
    /// </summary>
    public interface IPlatformService
    {
        PlatformId Platform { get; }
        bool IsInitialized { get; }

        /// <summary>Код языка из SDK площадки, например "ru" или "en".</summary>
        string Language { get; }

        /// <summary>Тип устройства игрока. Надёжен только после InitializeAsync.</summary>
        DeviceKind Device { get; }

        bool IsAuthorized { get; }
        string PlayerId { get; }
        string PlayerName { get; }

        /// <summary>Инициализация SDK. Вызывается один раз при старте, до любых других вызовов.</summary>
        UniTask InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>Игра загрузилась и показала первый экран. Яндекс требует этот вызов для модерации.</summary>
        void NotifyGameReady();

        /// <summary>Игрок начал играть (закрыл меню, начал раунд).</summary>
        void NotifyGameplayStart();

        /// <summary>Игрок вышел в меню или показана реклама.</summary>
        void NotifyGameplayStop();

        /// <summary>Запросить авторизацию. Возвращает true, если игрок авторизован.</summary>
        UniTask<bool> AuthorizeAsync(CancellationToken cancellationToken = default);
    }
}
