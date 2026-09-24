using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKPlay
{
    /// <summary>
    /// VK Play (vkplay.ru), раздел браузерных игр. У площадки свой API и своя авторизация,
    /// это не VK Bridge: VK Bridge нужен Играм ВКонтакте, см. Platform/VKGames.
    /// TODO: подключить API VK Play по документации из кабинета разработчика.
    /// </summary>
    public sealed class VKPlayPlatformService : IPlatformService
    {
        public PlatformId Platform => PlatformId.VKPlay;
        public bool IsInitialized { get; private set; }
        public string Language { get; private set; } = "ru";
        // TODO: брать тип устройства из API VK Play, если он его сообщает.
        public DeviceKind Device => DeviceKinds.FromUnity();
        public bool IsAuthorized { get; private set; }
        public string PlayerId { get; private set; }
        public string PlayerName { get; private set; }

        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] SDK not integrated yet, running as no-op");
            IsInitialized = true;
            return UniTask.CompletedTask;
        }

        public void NotifyGameReady() => Debug.Log("[VKPlay] GameReady");
        public void NotifyGameplayStart() => Debug.Log("[VKPlay] GameplayStart");
        public void NotifyGameplayStop() => Debug.Log("[VKPlay] GameplayStop");

        public UniTask<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] AuthorizeAsync: SDK not integrated yet");
            return UniTask.FromResult(false);
        }
    }
}
