using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.RuStore
{
    /// <summary>
    /// У RuStore нет лидербордов. TODO: свой бэкенд или локальная таблица рекордов.
    /// </summary>
    public sealed class RuStoreLeaderboardService : ILeaderboardService
    {
        private static readonly IReadOnlyList<LeaderboardEntry> Empty = new LeaderboardEntry[0];

        public bool IsAvailable => false;

        public UniTask SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken = default)
        {
            Debug.Log($"[RuStore] SubmitScore '{boardId}' {score}: no leaderboard backend");
            return UniTask.CompletedTask;
        }

        public UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string boardId, int count, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(Empty);
        }

        public UniTask<LeaderboardEntry> GetPlayerEntryAsync(string boardId, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult<LeaderboardEntry>(null);
        }
    }
}
