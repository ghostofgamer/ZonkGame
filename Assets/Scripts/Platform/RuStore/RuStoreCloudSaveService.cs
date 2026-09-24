using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.RuStore
{
    /// <summary>
    /// У RuStore нет облачных сохранений. Пока сохраняем локально в PlayerPrefs.
    /// TODO: при необходимости подключить свой бэкенд.
    /// </summary>
    public sealed class RuStoreCloudSaveService : ICloudSaveService
    {
        private const string Key = "base.save";

        public bool IsAvailable => true;

        public UniTask SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        public UniTask<string> LoadAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null);
        }
    }
}
