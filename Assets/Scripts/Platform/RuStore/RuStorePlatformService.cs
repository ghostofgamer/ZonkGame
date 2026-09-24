using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.RuStore
{
    /// <summary>
    /// RuStore, нативный Android. У RuStore нет единого игрового SDK:
    /// платежи через RuStore Billing SDK, реклама через отдельную сеть (Yandex Mobile Ads или myTarget).
    /// Авторизации в смысле web-площадок нет, PlayerId берётся из локального профиля или своего бэкенда.
    /// </summary>
    public sealed class RuStorePlatformService : IPlatformService
    {
        public PlatformId Platform => PlatformId.RuStore;
        public bool IsInitialized { get; private set; }
        public string Language => Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
        // Планшеты пока не отличаем от телефонов: для этого нужен размер экрана в дюймах.
        public DeviceKind Device => DeviceKind.Mobile;
        public bool IsAuthorized => false;
        public string PlayerId => null;
        public string PlayerName => null;

        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[RuStore] SDKs not integrated yet, running as no-op");
            IsInitialized = true;
            return UniTask.CompletedTask;
        }

        public void NotifyGameReady() => Debug.Log("[RuStore] GameReady");
        public void NotifyGameplayStart() => Debug.Log("[RuStore] GameplayStart");
        public void NotifyGameplayStop() => Debug.Log("[RuStore] GameplayStop");

        public UniTask<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(false);
        }
    }
}
