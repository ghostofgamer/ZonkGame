using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKPlay
{
    /// <summary>
    /// Облачное сохранение VK Play. TODO: VKWebAppStorageSet / VKWebAppStorageGet.
    /// У VK есть лимит на размер значения, при необходимости резать на несколько ключей.
    /// </summary>
    public sealed class VKPlayCloudSaveService : ICloudSaveService
    {
        public bool IsAvailable => false;

        public UniTask SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] Save: SDK not integrated yet");
            return UniTask.CompletedTask;
        }

        public UniTask<string> LoadAsync(CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] Load: SDK not integrated yet");
            return UniTask.FromResult<string>(null);
        }
    }
}
