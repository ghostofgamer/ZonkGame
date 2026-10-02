using System;
using System.Collections.Generic;
using System.Threading;
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
    /// Вкладка «Таланты» профиля: свободные очки, три ветки (Купец, Мастер, Коллекционер) столбцами узлов
    /// (деталь Parts/TalentNode), подробности выбранного узла, «Изучить» и «Сбросить» за монеты (GameConfig.TalentResetCoins).
    /// Титул игрока (талант Title) — к заголовку окна: название ветки, в которую вложено больше всего.
    /// Строится при открытии и после изучения (редко: память выделять можно). Без полей в префабе вкладки нет.
    /// </summary>
    public sealed partial class ProfileWindow
    {
        [Header("Таланты (необязательно)")]
        [SerializeField] private UiButtonView _talentsTab;
        [SerializeField] private GameObject _talentsPage;
        [SerializeField] private TMP_Text _talentPoints;
        [SerializeField] private TMP_Text[] _branchTitles = new TMP_Text[3];
        [SerializeField] private RectTransform[] _branchColumns = new RectTransform[3];
        [SerializeField] private TalentNodeView _talentTemplate;
        [SerializeField] private TMP_Text _talentInfo;
        [SerializeField] private UiButtonView _talentLearn;
        [SerializeField] private UiButtonView _talentReset;

        [Tooltip("Значки веток: Купец, Мастер, Коллекционер (вершина — тот же значок крупнее)")]
        [SerializeField] private Sprite[] _branchIcons = new Sprite[3];

        private ITalents _talents;
        private IWallet _wallet;
        private IUiService _ui;
        private GameConfig _gameConfig;
        private TalentConfig _selectedTalent;
        private bool _talentBusy;
        private readonly List<(TalentConfig talent, TalentNodeView view)> _talentNodes = new List<(TalentConfig, TalentNodeView)>();

        private static readonly string[] BranchKeys = { "talent.branch.merchant", "talent.branch.master", "talent.branch.collector" };

#if UNITY_EDITOR
        public void EditorSetupTalents(UiButtonView tab, GameObject page, TMP_Text points, TMP_Text[] branchTitles,
            RectTransform[] branchColumns, TalentNodeView template, TMP_Text info, UiButtonView learn, UiButtonView reset,
            Sprite[] branchIcons)
        {
            _talentsTab = tab;
            _talentsPage = page;
            _talentPoints = points;
            _branchTitles = branchTitles;
            _branchColumns = branchColumns;
            _talentTemplate = template;
            _talentInfo = info;
            _talentLearn = learn;
            _talentReset = reset;
            _branchIcons = branchIcons;
        }
#endif

        [Inject]
        public void ConstructTalents([InjectOptional] ITalents talents, [InjectOptional] IWallet wallet, [InjectOptional] IUiService ui,
            GameConfig config)
        {
            _talents = talents;
            _wallet = wallet;
            _ui = ui;
            _gameConfig = config;
        }

        private bool HasTalents => _talentsPage != null && _talentTemplate != null && _talents != null && _branchColumns != null &&
                                   _branchColumns.Length >= 3;

        private void AwakeTalents()
        {
            if (_talentTemplate != null && _talentTemplate.gameObject.scene.IsValid())
                _talentTemplate.gameObject.SetActive(false);
            if (_talentsTab != null)
                _talentsTab.OnClick(() => ShowPage(Page.Talents));
            if (_talentLearn != null)
                _talentLearn.OnClick(LearnSelected);
            if (_talentReset != null)
                _talentReset.OnClick(() => ResetTalentsAsync(this.GetCancellationTokenOnDestroy()).Forget());
        }

        private void ShowingTalents()
        {
            if (_talentsTab != null)
                _talentsTab.SetText(T("profile.talents"));
            var title = TitleText();
            if (!string.IsNullOrEmpty(title))
                _title.text = T("profile.title") + " · " + title;
        }

        /// <summary>Титул: ветка, в которую вложено больше всего (если взят талант Title).</summary>
        private string TitleText()
        {
            if (_talents == null || _talents.Value(TalentEffect.Title) <= 0f)
                return null;

            var best = TalentBranch.Merchant;
            var bestPoints = -1;
            foreach (TalentBranch branch in Enum.GetValues(typeof(TalentBranch)))
            {
                var points = _talents.PointsIn(branch);
                if (points > bestPoints)
                {
                    best = branch;
                    bestPoints = points;
                }
            }

            return T("talent.title." + best.ToString().ToLowerInvariant());
        }

        private void BuildTalents()
        {
            foreach (var (_, view) in _talentNodes)
                Destroy(view.gameObject);
            _talentNodes.Clear();

            for (var i = 0; i < 3 && _branchTitles != null && i < _branchTitles.Length; i++)
            {
                if (_branchTitles[i] != null)
                    _branchTitles[i].text = T(BranchKeys[i]) + "  " + _talents.PointsIn((TalentBranch)i);
            }

            foreach (var talent in _talents.All)
            {
                var column = _branchColumns[Mathf.Clamp((int)talent.Branch, 0, 2)];
                if (column == null)
                    continue;

                var view = Instantiate(_talentTemplate, column);
                view.gameObject.SetActive(true);
                var captured = talent;
                view.Clicked += () => SelectTalent(captured);
                _talentNodes.Add((talent, view));
            }

            if (_selectedTalent == null && _talents.All.Count > 0)
                _selectedTalent = _talents.All[0];
            RefreshTalents();
        }

        private void RefreshTalents()
        {
            if (_talentPoints != null)
                _talentPoints.text = T("talent.points", _talents.FreePoints, _talents.TotalPoints);

            foreach (var (talent, view) in _talentNodes)
            {
                var rank = _talents.RankOf(talent);
                var open = _talents.IsOpen(talent);
                Color background;
                if (rank >= talent.MaxRank)
                    background = _palette.RarityLegendary;
                else if (rank > 0)
                    background = _palette.Good;
                else if (open)
                    background = _palette.ButtonMuted;
                else
                    background = _palette.Locked;
                background.a = rank > 0 || open ? 0.85f : 0.45f;

                var icon = IconOf(talent);
                view.Show(icon, T(talent.NameKey), rank, talent.MaxRank, background, open || rank > 0 ? Color.white : _palette.Locked,
                    talent == _selectedTalent);
            }

            ShowTalentInfo();

            if (_talentReset != null)
            {
                var cost = _gameConfig != null ? _gameConfig.TalentResetCoins : 0;
                _talentReset.SetText(T("talent.reset", cost));
                _talentReset.SetVisible(_talents.SpentPoints > 0);
            }
        }

        private Sprite IconOf(TalentConfig talent)
        {
            if (talent.Icon != null)
                return talent.Icon;
            var index = (int)talent.Branch;
            return _branchIcons != null && index >= 0 && index < _branchIcons.Length ? _branchIcons[index] : null;
        }

        private void SelectTalent(TalentConfig talent)
        {
            _selectedTalent = talent;
            RefreshTalents();
        }

        private void ShowTalentInfo()
        {
            var talent = _selectedTalent;
            if (talent == null)
            {
                if (_talentInfo != null)
                    _talentInfo.text = T("talent.pick");
                if (_talentLearn != null)
                    _talentLearn.SetVisible(false);
                return;
            }

            var rank = _talents.RankOf(talent);
            var text = "<b>" + T(talent.NameKey) + "</b>  " + rank + "/" + talent.MaxRank + "\n" + Describe(talent);
            if (!_talents.IsOpen(talent))
            {
                if (talent.Requires != null && _talents.RankOf(talent.Requires) <= 0)
                    text += "\n" + T("talent.requires", T(talent.Requires.NameKey));
                if (_talents.PointsIn(talent.Branch) < talent.BranchPointsRequired)
                    text += "\n" + T("talent.requiresPoints", talent.BranchPointsRequired, T(BranchKeys[(int)talent.Branch]));
            }

            if (_talentInfo != null)
                _talentInfo.text = text;

            if (_talentLearn != null)
            {
                _talentLearn.SetVisible(rank < talent.MaxRank);
                _talentLearn.Interactable = _talents.CanRankUp(talent);
                _talentLearn.SetText(T("talent.learn", Math.Max(1, talent.CostPerRank)));
            }
        }

        /// <summary>Описание: {0} — значение за ранг, {1} — на максимуме.</summary>
        private string Describe(TalentConfig talent)
        {
            if (string.IsNullOrEmpty(talent.DescriptionKey))
                return string.Empty;

            var perRank = FormatValue(talent.ValuePerRank);
            var max = FormatValue(talent.ValuePerRank * talent.MaxRank);
            return T(talent.DescriptionKey, perRank, max);
        }

        private static string FormatValue(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void LearnSelected()
        {
            if (_selectedTalent == null || !_talents.TryRankUp(_selectedTalent))
                return;

            if (_kit != null)
                Toast.ShowAsync(_kit, transform, T("talent.learned", T(_selectedTalent.NameKey)), _palette.Gold, 0.8f,
                    this.GetCancellationTokenOnDestroy()).Forget();
            BuildTalents();
            ShowingTalents();
        }

        private async UniTaskVoid ResetTalentsAsync(CancellationToken ct)
        {
            if (_talentBusy || _talents.SpentPoints <= 0)
                return;

            _talentBusy = true;
            try
            {
                var cost = _gameConfig != null ? _gameConfig.TalentResetCoins : 0;
                if (_ui != null && !await ConfirmWindow.AskAsync(_ui, T("talent.resetAsk", cost), ct))
                    return;

                var coins = _gameConfig != null ? _gameConfig.Coins : null;
                if (cost > 0 && (coins == null || _wallet == null || !_wallet.TrySpend(coins, cost)))
                {
                    if (_kit != null)
                        Toast.ShowAsync(_kit, transform, T("talent.resetNoCoins"), _palette.Bad, 1f, ct).Forget();
                    return;
                }

                _talents.Reset();
                _title.text = T("profile.title");
                BuildTalents();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _talentBusy = false;
            }
        }
    }
}
