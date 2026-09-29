using System;
using System.Collections.Generic;
using System.Threading;
using Base.Platform;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Рекорды: вкладка на каждую таблицу (LeaderboardConfig), лучшие игроки и место игрока. Данные — площадки
    /// (ILeaderboardService); свой результат отправляет PlayerStats после партий. Закрытие — кнопка «Назад».
    /// </summary>
    public sealed class LeaderboardWindow : UiWindow
    {
        private const int TopCount = 10;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _tabs;
        [SerializeField] private UiButtonView _tabTemplate;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private RectTransform _list;
        [SerializeField] private TMP_Text _rowTemplate;

        [Tooltip("Префаб строки (Prefabs/UI/Parts/LeaderboardRow). Пусто — старый текстовый шаблон _rowTemplate")]
        [SerializeField] private LeaderboardRowView _rowPrefab;
        [SerializeField] private TMP_Text _me;
        [SerializeField] private UiButtonView _back;

        private readonly List<UiButtonView> _tabButtons = new List<UiButtonView>();
        private readonly List<GameObject> _rows = new List<GameObject>();
        private ContentDatabase _content;
        private ILeaderboardService _leaderboards;
        private PlayerStats _stats;
        private List<LeaderboardConfig> _boards;
        private CancellationTokenSource _loadCts;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform tabs, UiButtonView tabTemplate, TMP_Text status, RectTransform list,
            TMP_Text rowTemplate, TMP_Text me, UiButtonView back, LeaderboardRowView rowPrefab)
        {
            _rowPrefab = rowPrefab;
            _title = title;
            _tabs = tabs;
            _tabTemplate = tabTemplate;
            _status = status;
            _list = list;
            _rowTemplate = rowTemplate;
            _me = me;
            _back = back;
        }
#endif

        [Inject]
        public void Construct(ContentDatabase content, ILeaderboardService leaderboards, PlayerStats stats)
        {
            _content = content;
            _leaderboards = leaderboards;
            _stats = stats;
        }

        private void Awake()
        {
            _tabTemplate.gameObject.SetActive(false);
            if (_rowTemplate != null)
                _rowTemplate.gameObject.SetActive(false);
            _back.OnClick(RequestClose);
        }

        private void OnDestroy()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
        }

        protected override void OnShowing()
        {
            _title.text = T("leaderboard.title");
            _back.SetText(T("ui.back"));

            _boards = _content.All<LeaderboardConfig>();
            _boards.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var board in _boards)
            {
                var captured = board;
                var tab = _tabTemplate.Spawn(_tabs);
                tab.SetText(T(board.NameKey));
                tab.OnClick(() => Show(captured));
                _tabButtons.Add(tab);
            }

            // Свежий результат — до чтения таблицы.
            _stats.SubmitAsync(this.GetCancellationTokenOnDestroy()).Forget();

            if (_boards.Count > 0)
                Show(_boards[0]);
            else
                _status.text = T("leaderboard.unavailable");
        }

        private void Show(LeaderboardConfig board)
        {
            for (var i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].SetColor(_boards[i] == board ? UiColors.ButtonAccent : UiColors.ButtonMuted);

            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            LoadAsync(board, _loadCts.Token).Forget();
        }

        private async UniTaskVoid LoadAsync(LeaderboardConfig board, CancellationToken ct)
        {
            foreach (var row in _rows)
                Destroy(row);
            _rows.Clear();
            _me.text = T("leaderboard.mine", _stats.Value(board.Metric));

            if (!_leaderboards.IsAvailable)
            {
                _status.text = T("leaderboard.unavailable");
                return;
            }

            _status.text = T("leaderboard.loading");
            IReadOnlyList<LeaderboardEntry> top;
            LeaderboardEntry mine;
            try
            {
                top = await _leaderboards.GetTopAsync(board.TechnicalName, TopCount, ct);
                mine = await _leaderboards.GetPlayerEntryAsync(board.TechnicalName, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] {board.TechnicalName}: {e.Message}");
                if (this != null)
                    _status.text = T("leaderboard.unavailable");
                return;
            }

            if (this == null)
                return;

            _status.text = top == null || top.Count == 0 ? T("leaderboard.empty") : string.Empty;
            if (top != null)
            {
                foreach (var entry in top)
                {
                    var playerName = string.IsNullOrEmpty(entry.PlayerName) ? T("leaderboard.anonymous") : entry.PlayerName;
                    if (_rowPrefab != null)
                    {
                        var view = Instantiate(_rowPrefab, _list);
                        view.gameObject.SetActive(true);
                        view.Setup(entry.Rank, playerName, entry.Score, entry.IsCurrentPlayer);
                        _rows.Add(view.gameObject);
                        continue;
                    }

                    var row = Instantiate(_rowTemplate, _list);
                    row.gameObject.SetActive(true);
                    row.text = T("leaderboard.row", entry.Rank, playerName, entry.Score);
                    row.color = entry.IsCurrentPlayer ? UiColors.Gold : UiColors.Text;
                    _rows.Add(row.gameObject);
                }
            }

            if (mine != null)
                _me.text = T("leaderboard.myRank", mine.Rank, mine.Score);
        }
    }
}
