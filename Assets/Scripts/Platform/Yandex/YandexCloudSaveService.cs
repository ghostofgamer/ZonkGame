using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Yandex
{
    /// <summary>
    /// Облачное сохранение Яндекс Игр: player.setData и player.getData.
    /// Для неавторизованного игрока Яндекс хранит данные во временном профиле,
    /// поэтому сохранять можно всегда, но переносятся они только после авторизации.
    /// Лимит на объём данных 200 КБ, частота вызовов ограничена.
    /// </summary>
    public sealed class YandexCloudSaveService : ICloudSaveService
    {
        public bool IsAvailable => YandexBridge.IsSupported && YandexSession.IsInitialized;

        public async UniTask SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return;

            try
            {
                await YandexBridge.SaveAsync(json, cancellationToken);
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] Save failed: {e.Message}");
            }
        }

        public async UniTask<string> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return null;

            try
            {
                return await YandexBridge.LoadAsync(cancellationToken);
            }
            catch (YandexBridgeException e)
            {
                // Пробрасываем: "не прочиталось" нельзя путать с "сохранения нет",
                // иначе игра начнёт с нуля и затрёт прогресс игрока.
                Debug.LogWarning($"[Yandex] Load failed: {e.Message}");
                throw;
            }
        }
    }
}
