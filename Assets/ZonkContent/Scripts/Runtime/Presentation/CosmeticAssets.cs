using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zonk.Configs;

namespace Zonk.Presentation
{
    /// <summary>
    /// Префабы косметики по требованию (Addressables): локации, столы, стаканы, лампы грузятся, когда их ставят на стол,
    /// и выгружаются, когда их больше никто не показывает. Каждый показ — Acquire, конец показа — Release
    /// (счётчик на префаб). Предмет со старой прямой ссылкой (PrefabPayload.Prefab) отдаётся как есть, без счёта.
    ///
    /// Загрузка асинхронная (в браузере синхронной нет), поэтому то, что нужно сразу (стаканы на партию), заранее
    /// держат через AcquireAsync: тогда TryAcquire отдаёт префаб сразу.
    /// </summary>
    public sealed class CosmeticAssets : IDisposable
    {
        private sealed class Entry
        {
            public AsyncOperationHandle<GameObject> Handle;
            // Источник, а не Preserve: загрузку ждут несколько показов одновременно (Preserve — только по очереди).
            public UniTaskCompletionSource<GameObject> Loaded;
            public int Users;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        /// <summary>Префаб уже загружен (или это прямая ссылка): взять сразу. Иначе false — грузить через AcquireAsync.</summary>
        public bool TryAcquire(PrefabPayload payload, out GameObject prefab)
        {
            prefab = null;
            if (payload == null)
                return false;

            if (!payload.HasAsset)
            {
                prefab = payload.Prefab;
                return prefab != null;
            }

            if (!_entries.TryGetValue(payload.Asset.AssetGUID, out var entry) || !entry.Handle.IsDone ||
                entry.Handle.Status != AsyncOperationStatus.Succeeded)
                return false;

            entry.Users++;
            prefab = entry.Handle.Result;
            return true;
        }

        /// <summary>
        /// Загрузить (или взять загруженный) префаб. Вернул не null — потом свой Release; null (не загрузилось,
        /// отмена — исключение) — ничего не держится.
        /// </summary>
        public async UniTask<GameObject> AcquireAsync(PrefabPayload payload, CancellationToken ct)
        {
            if (payload == null)
                return null;
            if (!payload.HasAsset)
                return payload.Prefab;

            var key = payload.Asset.AssetGUID;
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry
                {
                    Handle = Addressables.LoadAssetAsync<GameObject>(payload.Asset.RuntimeKey),
                    Loaded = new UniTaskCompletionSource<GameObject>(),
                };
                _entries[key] = entry;
                CompleteAsync(entry).Forget();
            }

            entry.Users++;
            try
            {
                return await entry.Loaded.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                Release(payload);
                throw;
            }
            catch (Exception e)
            {
                // Не загрузилось (нет сборки Addressables, битая ссылка): предмет не показывается, игра идёт дальше.
                Debug.LogError($"[Cosmetics] Prefab {key} failed to load: {e.Message}");
                Release(payload);
                return null;
            }
        }

        /// <summary>Единственный, кто ждёт саму загрузку: передаёт результат всем ожидающим показам.</summary>
        private static async UniTaskVoid CompleteAsync(Entry entry)
        {
            try
            {
                entry.Loaded.TrySetResult(await entry.Handle.ToUniTask());
            }
            catch (Exception e)
            {
                entry.Loaded.TrySetException(e);
            }
        }

        /// <summary>Конец показа. Когда показов не осталось, префаб выгружается.</summary>
        public void Release(PrefabPayload payload)
        {
            if (payload == null || !payload.HasAsset || !_entries.TryGetValue(payload.Asset.AssetGUID, out var entry))
                return;

            entry.Users--;
            if (entry.Users > 0)
                return;

            _entries.Remove(payload.Asset.AssetGUID);
            if (entry.Handle.IsValid())
                Addressables.Release(entry.Handle);
        }

        public void Dispose()
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Handle.IsValid())
                    Addressables.Release(entry.Handle);
            }

            _entries.Clear();
        }
    }
}
