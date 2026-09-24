using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Yandex
{
    /// <summary>
    /// Яндекс Игры: инициализация SDK, сигналы жизненного цикла, авторизация.
    /// Работает через YandexBridge.jslib, поэтому что-то делает только в WebGL-билде.
    /// </summary>
    public sealed class YandexPlatformService : IPlatformService
    {
        public PlatformId Platform => PlatformId.Yandex;
        public bool IsInitialized => YandexSession.IsInitialized;
        public string Language => YandexSession.Language;
        public DeviceKind Device => YandexSession.Device != DeviceKind.Unknown ? YandexSession.Device : DeviceKinds.FromUnity();
        public bool IsAuthorized => YandexSession.IsAuthorized;
        public string PlayerId => YandexSession.PlayerId;
        public string PlayerName => YandexSession.PlayerName;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!YandexBridge.IsSupported)
            {
                Debug.LogWarning("[Yandex] SDK is available only in a WebGL build, skipping initialization");
                return;
            }

            try
            {
                var info = await YandexBridge.InitializeAsync(cancellationToken);
                YandexSession.Apply(info);
                Debug.Log($"[Yandex] Initialized: lang={Language} device={Device} authorized={IsAuthorized} id={PlayerId ?? "-"}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Yandex] Initialization failed: {e.Message}");
                throw;
            }
        }

        public void NotifyGameReady()
        {
            YandexBridge.NotifyGameReady();
        }

        public void NotifyGameplayStart()
        {
            YandexBridge.NotifyGameplayStart();
        }

        public void NotifyGameplayStop()
        {
            YandexBridge.NotifyGameplayStop();
        }

        public async UniTask<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            if (!YandexBridge.IsSupported)
                return false;

            try
            {
                var info = await YandexBridge.AuthorizeAsync(cancellationToken);
                YandexSession.Apply(info);
                return IsAuthorized;
            }
            catch (YandexBridgeException e)
            {
                // Игрок закрыл окно авторизации: это обычный сценарий, не ошибка.
                Debug.Log($"[Yandex] Authorization declined: {e.Message}");
                return false;
            }
        }
    }
}
