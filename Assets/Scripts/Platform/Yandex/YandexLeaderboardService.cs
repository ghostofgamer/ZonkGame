using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Yandex
{
    /// <summary>
    /// Лидерборды Яндекс Игр. Отправка очков и своя запись доступны только
    /// авторизованному игроку, топ читается всегда.
    /// Имя таблицы задаётся в консоли разработчика и передаётся как boardId.
    /// </summary>
    public sealed class YandexLeaderboardService : ILeaderboardService
    {
        private static readonly IReadOnlyList<LeaderboardEntry> NoEntries = new LeaderboardEntry[0];

        public bool IsAvailable => YandexBridge.IsSupported && YandexSession.IsInitialized;

        public async UniTask SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return;

            if (!YandexSession.IsAuthorized)
            {
                Debug.Log("[Yandex] SubmitScore skipped: player is not authorized");
                return;
            }

            try
            {
                await YandexBridge.SubmitScoreAsync(boardId, score, cancellationToken);
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] SubmitScore failed: {e.Message}");
            }
        }

        public async UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string boardId, int count, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return NoEntries;

            try
            {
                var response = await YandexBridge.GetTopAsync(boardId, count, cancellationToken);
                if (response.items == null)
                    return NoEntries;

                var entries = new List<LeaderboardEntry>(response.items.Length);
                foreach (var item in response.items)
                {
                    entries.Add(new LeaderboardEntry
                    {
                        Rank = item.rank,
                        Score = (long)item.score,
                        PlayerName = item.playerName,
                        IsCurrentPlayer = item.isCurrentPlayer,
                    });
                }

                return entries;
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] GetTop failed: {e.Message}");
                return NoEntries;
            }
        }

        public async UniTask<LeaderboardEntry> GetPlayerEntryAsync(string boardId, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable || !YandexSession.IsAuthorized)
                return null;

            try
            {
                var entry = await YandexBridge.GetPlayerEntryAsync(boardId, cancellationToken);
                if (entry == null || !entry.found)
                    return null;

                return new LeaderboardEntry
                {
                    Rank = entry.rank,
                    Score = (long)entry.score,
                    PlayerName = entry.playerName,
                    IsCurrentPlayer = true,
                };
            }
            catch (YandexBridgeException e)
            {
                Debug.LogWarning($"[Yandex] GetPlayerEntry failed: {e.Message}");
                return null;
            }
        }
    }
}
