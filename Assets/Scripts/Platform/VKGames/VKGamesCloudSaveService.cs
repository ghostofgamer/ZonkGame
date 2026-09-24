using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Облачное сохранение через хранилище VK (VKWebAppStorageSet / VKWebAppStorageGet).
    /// Данные привязаны к пользователю VK, а не к устройству.
    ///
    /// Одно значение хранилища вмещает около 2000 символов, поэтому мост режет JSON на куски
    /// и пишет их в один из двух слотов по очереди, а номер слота в конце. Лимит VK: 1000 вызовов
    /// в час на пользователя, одно сохранение стоит (число кусков по 1000 символов + 1) вызовов.
    /// Сохранять на значимых событиях, а не каждый кадр или каждое действие.
    /// </summary>
    public sealed class VKGamesCloudSaveService : ICloudSaveService
    {
        public bool IsAvailable => VKGamesBridge.IsSupported && VKGamesSession.IsInitialized;

        public async UniTask SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return;

            try
            {
                await VKGamesBridge.SaveAsync(json, cancellationToken);
            }
            catch (VKGamesBridgeException e)
            {
                Debug.LogWarning($"[VKGames] Save failed: {e.Message}");
            }
        }

        public async UniTask<string> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return null;

            try
            {
                return await VKGamesBridge.LoadAsync(cancellationToken);
            }
            catch (VKGamesBridgeException e)
            {
                // Пробрасываем: "не прочиталось" нельзя путать с "сохранения нет",
                // иначе игра начнёт с нуля и затрёт прогресс игрока.
                Debug.LogWarning($"[VKGames] Load failed: {e.Message}");
                throw;
            }
        }
    }
}
