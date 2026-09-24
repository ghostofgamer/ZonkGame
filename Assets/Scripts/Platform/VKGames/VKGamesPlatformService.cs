using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Игры ВКонтакте через VK Bridge. VKWebAppInit отправляет страница,
    /// здесь мост дожидается его и забирает параметры запуска: язык, платформу, id игрока.
    ///
    /// Отдельной авторизации нет: игру открывает пользователь VK, его id приходит в параметрах запуска.
    /// Аналогов LoadingAPI.ready и GameplayAPI.start/stop Яндекса у VK нет, эти вызовы ничего не делают.
    /// </summary>
    public sealed class VKGamesPlatformService : IPlatformService
    {
        public PlatformId Platform => PlatformId.VKGames;
        public bool IsInitialized => VKGamesSession.IsInitialized;
        public string Language => VKGamesSession.Language;
        public DeviceKind Device => VKGamesSession.Device != DeviceKind.Unknown ? VKGamesSession.Device : DeviceKinds.FromUnity();
        public bool IsAuthorized => VKGamesSession.UserId != null;
        public string PlayerId => VKGamesSession.UserId;
        public string PlayerName => VKGamesSession.UserName;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!VKGamesBridge.IsSupported)
            {
                Debug.LogWarning("[VKGames] VK Bridge is available only in a WebGL build, skipping initialization");
                return;
            }

            try
            {
                var info = await VKGamesBridge.InitializeAsync(cancellationToken);
                VKGamesSession.Apply(info);
                Debug.Log($"[VKGames] Initialized: lang={Language} vkPlatform={VKGamesSession.VkPlatform ?? "-"} " +
                          $"device={Device} user={PlayerId ?? "-"}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VKGames] Initialization failed: {e.Message}");
                throw;
            }
        }

        public void NotifyGameReady() => Debug.Log("[VKGames] GameReady: no VK equivalent");
        public void NotifyGameplayStart() => Debug.Log("[VKGames] GameplayStart: no VK equivalent");
        public void NotifyGameplayStop() => Debug.Log("[VKGames] GameplayStop: no VK equivalent");

        public UniTask<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(IsAuthorized);
        }
    }
}
