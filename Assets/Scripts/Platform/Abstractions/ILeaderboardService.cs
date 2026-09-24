using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Platform
{
    public sealed class LeaderboardEntry
    {
        public int Rank;
        public long Score;
        public string PlayerName;
        public bool IsCurrentPlayer;
    }

    /// <summary>Таблицы лидеров.</summary>
    public interface ILeaderboardService
    {
        bool IsAvailable { get; }

        UniTask SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken = default);

        UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string boardId, int count, CancellationToken cancellationToken = default);

        /// <summary>Запись текущего игрока или null, если игрок не авторизован или не в таблице.</summary>
        UniTask<LeaderboardEntry> GetPlayerEntryAsync(string boardId, CancellationToken cancellationToken = default);
    }
}
