using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Base.Platform;
using UnityEngine;
using Zenject;

namespace Base.Services.Saves
{
    /// <summary>
    /// Сохранение с разделами и версиями поверх ICloudSaveService.
    ///
    /// В облаке лежит конверт {format, sections:[{key, version, json}]}. При загрузке разделы
    /// читаются лениво: миграции применяются в момент первого Get. Разделы, которые никто не запросил,
    /// записываются обратно без изменений.
    ///
    /// Защита от потери прогресса:
    /// - если облако не прочиталось, запись в облако выключается до конца сессии;
    /// - сохранение старого формата (без конверта) и раздел, который не удалось разобрать,
    ///   откладываются под отдельным ключом, а не перезаписываются.
    /// </summary>
    public sealed class SaveStore : ISaveStore, IInitializable, IDisposable
    {
        private const int EnvelopeFormat = 1;
        private const string LegacyKey = "_legacy";
        private const string BrokenSuffix = "_broken";

        private const int LoadAttempts = 3;
        private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(5);

        private readonly ICloudSaveService _cloud;
        private readonly Dictionary<string, Dictionary<int, ISaveMigration>> _migrations =
            new Dictionary<string, Dictionary<int, ISaveMigration>>();
        private readonly Dictionary<string, int> _versions = new Dictionary<string, int>();

        /// <summary>Разделы в том виде, в каком пришли из облака.</summary>
        private readonly Dictionary<string, SaveSection> _raw = new Dictionary<string, SaveSection>();

        /// <summary>Разделы, которые игра уже запросила. Сериализуются при каждой записи.</summary>
        private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

        private readonly UniTaskCompletionSource _loaded = new UniTaskCompletionSource();
        private bool _loading;
        private bool _saving;
        private bool _saveScheduled;
        private bool _blockedWarningShown;

        public SaveStore(ICloudSaveService cloud, List<ISaveMigration> migrations)
        {
            _cloud = cloud;

            foreach (var migration in migrations)
            {
                if (!_migrations.TryGetValue(migration.Key, out var byVersion))
                    _migrations[migration.Key] = byVersion = new Dictionary<int, ISaveMigration>();

                if (byVersion.ContainsKey(migration.FromVersion))
                {
                    Debug.LogError($"[Saves] Two migrations for '{migration.Key}' from v{migration.FromVersion}");
                    continue;
                }

                byVersion[migration.FromVersion] = migration;
                _versions[migration.Key] = Math.Max(GetVersion(migration.Key), migration.FromVersion + 1);
            }
        }

        public bool IsLoaded { get; private set; }
        public bool IsCloudWriteBlocked { get; private set; }

        public void Initialize()
        {
            // Отложенная запись может не успеть, если игрок свернул игру или закрыл вкладку.
            Application.focusChanged += OnFocusChanged;
            Application.quitting += FlushPendingSave;
        }

        public void Dispose()
        {
            Application.focusChanged -= OnFocusChanged;
            Application.quitting -= FlushPendingSave;
        }

        public int GetVersion(string key)
        {
            return _versions.TryGetValue(key, out var version) ? version : 1;
        }

        public UniTask WaitLoadedAsync(CancellationToken cancellationToken = default)
        {
            return _loaded.Task.AttachExternalCancellation(cancellationToken);
        }

        public async UniTask LoadAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoaded || _loading)
            {
                await WaitLoadedAsync(cancellationToken);
                return;
            }

            _loading = true;
            try
            {
                if (!_cloud.IsAvailable)
                {
                    // Площадка не дала хранилище (например, VK вне vk.com): прогресс живёт только в памяти.
                    Debug.LogWarning("[Saves] Cloud save is unavailable, progress will not persist");
                }
                else
                {
                    var json = await ReadCloudAsync(cancellationToken);
                    if (!IsCloudWriteBlocked)
                        Parse(json);
                }
            }
            finally
            {
                _loading = false;
                IsLoaded = true;
                _loaded.TrySetResult();
            }

            Debug.Log($"[Saves] Loaded: sections={_raw.Count} cloudWriteBlocked={IsCloudWriteBlocked}");
        }

        public T Get<T>(string key) where T : class, new()
        {
            if (!IsLoaded)
                throw new InvalidOperationException($"Save section '{key}' requested before load. Await ISaveStore.WaitLoadedAsync first.");

            if (_cache.TryGetValue(key, out var cached))
            {
                if (cached is T typed)
                    return typed;

                throw new InvalidOperationException($"Save section '{key}' is already used as {cached.GetType().Name}, not {typeof(T).Name}");
            }

            var value = Restore<T>(key) ?? new T();
            _cache[key] = value;
            return value;
        }

        public void RequestSave()
        {
            if (_saveScheduled)
                return;

            _saveScheduled = true;
            DelayedSaveAsync().Forget();
        }

        public async UniTask SaveNowAsync(CancellationToken cancellationToken = default)
        {
            if (!IsLoaded)
            {
                Debug.LogWarning("[Saves] Save skipped: not loaded yet");
                return;
            }

            if (IsCloudWriteBlocked)
            {
                if (!_blockedWarningShown)
                {
                    _blockedWarningShown = true;
                    Debug.LogError("[Saves] Cloud write is blocked for this session: the cloud save could not be read");
                }
                return;
            }

            if (!_cloud.IsAvailable)
                return;

            // Записи идут строго по очереди, иначе старая могла бы лечь в облако после новой.
            while (_saving)
                await UniTask.Yield(cancellationToken);

            _saving = true;
            try
            {
                _saveScheduled = false;
                var json = Serialize();
                await _cloud.SaveAsync(json, cancellationToken);
                Debug.Log($"[Saves] Saved {json.Length} chars");
            }
            finally
            {
                _saving = false;
            }
        }

        private async UniTask<string> ReadCloudAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= LoadAttempts; attempt++)
            {
                try
                {
                    return await _cloud.LoadAsync(cancellationToken).Timeout(LoadTimeout, DelayType.Realtime);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Saves] Load attempt {attempt} failed: {e.Message}");
                }

                if (attempt < LoadAttempts)
                    await UniTask.Delay(TimeSpan.FromSeconds(attempt), ignoreTimeScale: true, cancellationToken: cancellationToken);
            }

            IsCloudWriteBlocked = true;
            Debug.LogError("[Saves] Cloud save could not be read, cloud write is blocked for this session");
            return null;
        }

        private void Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            SaveEnvelope envelope = null;
            try
            {
                envelope = JsonUtility.FromJson<SaveEnvelope>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Saves] Save is not an envelope: {e.Message}");
            }

            if (envelope == null || envelope.format <= 0 || envelope.sections == null)
            {
                // Сохранение старого формата: не выбрасываем, а откладываем целиком.
                _raw[LegacyKey] = new SaveSection { key = LegacyKey, version = 0, json = json };
                Debug.LogWarning($"[Saves] Unknown save format kept as '{LegacyKey}' ({json.Length} chars)");
                return;
            }

            if (envelope.format > EnvelopeFormat)
                Debug.LogWarning($"[Saves] Save format {envelope.format} is newer than {EnvelopeFormat}");

            foreach (var section in envelope.sections)
            {
                if (section != null && !string.IsNullOrEmpty(section.key))
                    _raw[section.key] = section;
            }
        }

        private T Restore<T>(string key) where T : class, new()
        {
            if (!_raw.TryGetValue(key, out var section) || string.IsNullOrEmpty(section.json))
                return null;

            try
            {
                var json = Migrate(key, section);
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                // Раздел будет перезаписан новым объектом, поэтому исходный текст откладываем.
                var brokenKey = key + BrokenSuffix;
                _raw[brokenKey] = new SaveSection { key = brokenKey, version = section.version, json = section.json };
                Debug.LogError($"[Saves] Section '{key}' v{section.version} could not be restored, kept as '{brokenKey}': {e.Message}");
                return null;
            }
        }

        private string Migrate(string key, SaveSection section)
        {
            var current = GetVersion(key);
            var version = Math.Max(section.version, 1);
            var json = section.json;

            if (version > current)
            {
                Debug.LogWarning($"[Saves] Section '{key}' v{version} is newer than this build (v{current})");
                return json;
            }

            while (version < current)
            {
                if (!_migrations.TryGetValue(key, out var byVersion) || !byVersion.TryGetValue(version, out var migration))
                    throw new InvalidOperationException($"no migration for '{key}' from v{version}");

                json = migration.Migrate(json);
                version++;
                Debug.Log($"[Saves] Section '{key}' migrated to v{version}");
            }

            return json;
        }

        private string Serialize()
        {
            var envelope = new SaveEnvelope { format = EnvelopeFormat, sections = new List<SaveSection>() };

            foreach (var pair in _raw)
            {
                if (!_cache.ContainsKey(pair.Key))
                    envelope.sections.Add(pair.Value);
            }

            foreach (var pair in _cache)
            {
                envelope.sections.Add(new SaveSection
                {
                    key = pair.Key,
                    version = GetVersion(pair.Key),
                    json = JsonUtility.ToJson(pair.Value),
                });
            }

            return JsonUtility.ToJson(envelope);
        }

        private async UniTaskVoid DelayedSaveAsync()
        {
            await UniTask.Delay(SaveDelay, ignoreTimeScale: true);

            // Запись могла уже пройти через SaveNowAsync.
            if (!_saveScheduled)
                return;

            try
            {
                await SaveNowAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Saves] Save failed: {e.Message}");
            }
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
                FlushPendingSave();
        }

        private void FlushPendingSave()
        {
            if (_saveScheduled)
                SaveNowAsync().Forget();
        }
    }
}
