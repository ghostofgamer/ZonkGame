using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Лидерборды VK с клиента недоступны.
    ///
    /// Очки сохраняются только с сервера игры методом secure.addAppEvent с сервисным ключом,
    /// и только у игр, прошедших модерацию. Ключ нельзя класть в клиент.
    /// VKWebAppShowLeaderBoardBox лишь показывает окно таблицы и очки не сохраняет.
    /// Когда появится свой сервер, SubmitScoreAsync пойдёт через него,
    /// а топ можно будет читать через apps.getLeaderboard.
    /// </summary>
    public sealed class VKGamesLeaderboardService : ILeaderboardService
    {
        private static readonly IReadOnlyList<LeaderboardEntry> NoEntries = new LeaderboardEntry[0];

        public bool IsAvailable => false;

        public UniTask SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken = default)
        {
            Debug.Log($"[VKGames] SubmitScore skipped: VK leaderboards require a game server (secure.addAppEvent)");
            return UniTask.CompletedTask;
        }

        public UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string boardId, int count, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult(NoEntries);
        }

        public UniTask<LeaderboardEntry> GetPlayerEntryAsync(string boardId, CancellationToken cancellationToken = default)
        {
            return UniTask.FromResult<LeaderboardEntry>(null);
        }
    }
}
