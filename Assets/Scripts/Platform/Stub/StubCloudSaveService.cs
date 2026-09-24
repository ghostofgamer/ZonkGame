using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Stub
{
    /// <summary>Сохранение в PlayerPrefs вместо облака.</summary>
    public sealed class StubCloudSaveService : ICloudSaveService
    {
        private const string Key = "base.stub.save";

        public bool IsAvailable => true;

        public UniTask SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
            Debug.Log($"[Stub] Saved {json?.Length ?? 0} chars");
            return UniTask.CompletedTask;
        }

        public UniTask<string> LoadAsync(CancellationToken cancellationToken = default)
        {
            var json = PlayerPrefs.HasKey(Key) ? PlayerPrefs.GetString(Key) : null;
            Debug.Log(json == null ? "[Stub] No save found" : $"[Stub] Loaded {json.Length} chars");
            return UniTask.FromResult(json);
        }
    }
}
