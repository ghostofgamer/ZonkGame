using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.Stub
{
    /// <summary>Таблица лидеров в памяти: несколько ботов плюс лучший результат игрока.</summary>
    public sealed class StubLeaderboardService : ILeaderboardService
    {
        private readonly Dictionary<string, long> _playerBest = new Dictionary<string, long>();

        public bool IsAvailable => true;

        public UniTask SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken = default)
        {
            if (!_playerBest.TryGetValue(boardId, out var best) || score > best)
                _playerBest[boardId] = score;

            Debug.Log($"[Stub] Leaderboard '{boardId}' submit {score}");
            return UniTask.CompletedTask;
        }

        public UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string boardId, int count, CancellationToken cancellationToken = default)
        {
            var entries = new List<LeaderboardEntry>
            {
                new LeaderboardEntry { Score = 12000, PlayerName = "Bot Alpha" },
                new LeaderboardEntry { Score = 9500, PlayerName = "Bot Beta" },
                new LeaderboardEntry { Score = 7000, PlayerName = "Bot Gamma" },
            };

            if (_playerBest.TryGetValue(boardId, out var best))
                entries.Add(new LeaderboardEntry { Score = best, PlayerName = "Stub Player", IsCurrentPlayer = true });

            entries.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (var i = 0; i < entries.Count; i++)
                entries[i].Rank = i + 1;

            if (entries.Count > count)
                entries.RemoveRange(count, entries.Count - count);

            return UniTask.FromResult<IReadOnlyList<LeaderboardEntry>>(entries);
        }

        public async UniTask<LeaderboardEntry> GetPlayerEntryAsync(string boardId, CancellationToken cancellationToken = default)
        {
            var top = await GetTopAsync(boardId, int.MaxValue, cancellationToken);
            foreach (var entry in top)
                if (entry.IsCurrentPlayer)
                    return entry;
            return null;
        }
    }
}
