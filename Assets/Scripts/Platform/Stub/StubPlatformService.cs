using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Stub
{
    /// <summary>
    /// Заглушка площадки для редактора и локальных сборок без SDK.
    /// Всё "успешно" сразу, действия пишутся в консоль.
    /// </summary>
    public sealed class StubPlatformService : IPlatformService
    {
        public PlatformId Platform => PlatformId.Stub;
        public bool IsInitialized { get; private set; }
        public string Language => Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
        public DeviceKind Device => DeviceKinds.FromUnity();
        public bool IsAuthorized { get; private set; }
        public string PlayerId => IsAuthorized ? "stub-player" : null;
        public string PlayerName => IsAuthorized ? "Stub Player" : null;

        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            Debug.Log("[Stub] Platform initialized");
            return UniTask.CompletedTask;
        }

        public void NotifyGameReady() => Debug.Log("[Stub] GameReady");
        public void NotifyGameplayStart() => Debug.Log("[Stub] GameplayStart");
        public void NotifyGameplayStop() => Debug.Log("[Stub] GameplayStop");

        public UniTask<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            IsAuthorized = true;
            Debug.Log("[Stub] Authorized");
            return UniTask.FromResult(true);
        }
    }
}
