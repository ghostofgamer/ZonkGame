using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKPlay
{
    /// <summary>
    /// Лидерборды VK Play. TODO: VKWebAppShowLeaderBoardBox показывает только встроенное окно VK,
    /// свой список для UI придётся хранить на своём бэкенде.
    /// </summary>
    public sealed class VKPlayLeaderboardService : ILeaderboardService
    {
        private static readonly IReadOnlyList<LeaderboardEntry> Empty = new LeaderboardEntry[0];

        public bool IsAvailable => false;

        public UniTask SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken = default)
        {
            Debug.LogWarning("[VKPlay] SubmitScore: SDK not integrated yet");
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
