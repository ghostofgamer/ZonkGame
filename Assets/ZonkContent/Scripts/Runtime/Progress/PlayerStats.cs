using System;
using System.Collections.Generic;
using System.Threading;
using Base.Platform;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Progress
{
    [Serializable]
    public sealed class StatsSave
    {
        public int CampaignWins;
        public int BestTurn;

        /// <summary>Что уже отправлено в таблицы (ID таблицы → значение): повторно то же не отправляется.</summary>
        public List<SubmittedScore> Submitted = new List<SubmittedScore>();
    }

    [Serializable]
    public sealed class SubmittedScore
    {
        public string Id;
        public long Value;
    }

    /// <summary>
    /// Статистика игрока для таблиц лидеров и профиля. После партии кампании обновляется и отправляется
    /// в таблицы (LeaderboardConfig) — только когда значение выросло: у площадок ограничена частота записи.
    /// </summary>
    public sealed class PlayerStats
    {
        private readonly ISaveStore _saves;
        private readonly ContentDatabase _content;
        private readonly ICampaignProgress _campaign;
        private readonly ILeaderboardService _leaderboards;

        public PlayerStats(ISaveStore saves, ContentDatabase content, ICampaignProgress campaign, ILeaderboardService leaderboards)
        {
            _saves = saves;
            _content = content;
            _campaign = campaign;
            _leaderboards = leaderboards;
        }

        private StatsSave Data => _saves.Get<StatsSave>(SaveKeys.Stats);

        public bool IsLeaderboardAvailable => _leaderboards.IsAvailable;

        public long Value(LeaderboardMetric metric)
        {
            switch (metric)
            {
                case LeaderboardMetric.TotalStars: return _campaign.TotalStars;
                case LeaderboardMetric.CampaignWins: return Data.CampaignWins;
                case LeaderboardMetric.BestTurn: return Data.BestTurn;
                default: return 0;
            }
        }

        /// <summary>Итог партии против соперника: победа и лучший ход игрока. Затем отправка в таблицы.</summary>
        public void RecordCampaignMatch(bool won, int bestTurn)
        {
            var data = Data;
            if (won)
                data.CampaignWins++;
            data.BestTurn = Math.Max(data.BestTurn, bestTurn);
            _saves.RequestSave();
            SubmitAsync(CancellationToken.None).Forget();
        }

        /// <summary>Отправить в таблицы то, что выросло с прошлой отправки.</summary>
        public async UniTaskVoid SubmitAsync(CancellationToken ct)
        {
            if (!_leaderboards.IsAvailable)
                return;

            foreach (var board in _content.All<LeaderboardConfig>())
            {
                if (string.IsNullOrEmpty(board.TechnicalName))
                    continue;

                var value = Value(board.Metric);
                var data = Data;
                var sent = data.Submitted.Find(s => s.Id == board.Id);
                if (value <= 0 || (sent != null && sent.Value >= value))
                    continue;

                try
                {
                    await _leaderboards.SubmitScoreAsync(board.TechnicalName, value, ct);
                    if (sent == null)
                        data.Submitted.Add(sent = new SubmittedScore { Id = board.Id });
                    sent.Value = value;
                    _saves.RequestSave();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    // Таблица недоступна (нет входа, нет сети): отправится в следующий раз.
                    Debug.LogWarning($"[Leaderboard] {board.TechnicalName}: {e.Message}");
                }
            }
        }
    }
}
