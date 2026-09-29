using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Кампания: глава с листанием, список соперников (копии шаблона кнопки в прокрутке), описание выбранного
    /// соперника и его правила, мои кости, «Играть» с ценой энергии. Возвращает соперника или null (назад).
    /// </summary>
    public sealed class CampaignWindow : UiWindow
    {
        [SerializeField] private UiButtonView _previousChapter;
        [SerializeField] private UiButtonView _nextChapter;
        [SerializeField] private TMP_Text _chapterTitle;
        [SerializeField] private RectTransform _opponentList;
        [SerializeField] private UiButtonView _opponentTemplate;

        [Tooltip("Префаб строки соперника (Prefabs/UI/Parts/OpponentRow). Пусто — старый шаблон-кнопка _opponentTemplate")]
        [SerializeField] private OpponentRowView _opponentRowPrefab;

        [Tooltip("Куда ставятся условия звёзд выбранного соперника и префаб строки условия (Parts/StarConditionRow)")]
        [SerializeField] private RectTransform _conditionsList;
        [SerializeField] private StarConditionRowView _conditionRowPrefab;
        [SerializeField] private TMP_Text _conditionsTitle;
        [SerializeField] private TMP_Text _info;
        [SerializeField] private TMP_Text _myDiceLabel;
        [SerializeField] private DiceLoadoutView _myDice;
        [SerializeField] private DicePresetsView _presets;
        [SerializeField] private UnityEngine.UI.Image _portrait;
        [SerializeField] private TMP_Text _stakeLabel;
        [SerializeField] private RectTransform _stakeList;
        [SerializeField] private UiButtonView _stakeTemplate;
        [SerializeField] private UiButtonView _back;
        [SerializeField] private UiButtonView _play;

        private readonly Choice<OpponentConfig> _choice = new Choice<OpponentConfig>();
        private ICampaignProgress _progress;
        private GameConfig _config;
        private int _chapterIndex;
        private OpponentConfig _selected;
        private IDieMastery _mastery;
        private IWallet _wallet;
        private IInventory _inventory;
        private readonly List<UiButtonView> _stakeButtons = new List<UiButtonView>();

        /// <summary>Ставка монетами на партию (0 — без ставки). Списывается при начале партии.</summary>
        public int Stake { get; private set; }

        [Zenject.Inject]
        public void Construct(IDieMastery mastery, IWallet wallet, IInventory inventory)
        {
            _inventory = inventory;
            _mastery = mastery;
            _wallet = wallet;
        }

        public ChapterConfig Chapter { get; private set; }

#if UNITY_EDITOR
        public void EditorSetup(UiButtonView previousChapter, UiButtonView nextChapter, TMP_Text chapterTitle,
            RectTransform opponentList, UiButtonView opponentTemplate, TMP_Text info, TMP_Text myDiceLabel, DiceLoadoutView myDice,
            UiButtonView back, UiButtonView play, DicePresetsView presets, TMP_Text stakeLabel, RectTransform stakeList,
            UiButtonView stakeTemplate, UnityEngine.UI.Image portrait, OpponentRowView opponentRowPrefab, RectTransform conditionsList,
            StarConditionRowView conditionRowPrefab, TMP_Text conditionsTitle)
        {
            _opponentRowPrefab = opponentRowPrefab;
            _conditionsList = conditionsList;
            _conditionRowPrefab = conditionRowPrefab;
            _conditionsTitle = conditionsTitle;
            _portrait = portrait;
            _stakeLabel = stakeLabel;
            _stakeList = stakeList;
            _stakeTemplate = stakeTemplate;
            _presets = presets;
            _previousChapter = previousChapter;
            _nextChapter = nextChapter;
            _chapterTitle = chapterTitle;
            _opponentList = opponentList;
            _opponentTemplate = opponentTemplate;
            _info = info;
            _myDiceLabel = myDiceLabel;
            _myDice = myDice;
            _back = back;
            _play = play;
        }
#endif

        private void Awake()
        {
            if (_opponentTemplate != null)
                _opponentTemplate.gameObject.SetActive(false);
            _previousChapter.OnClick(() => ShowChapter(_chapterIndex - 1));
            _nextChapter.OnClick(() => ShowChapter(_chapterIndex + 1));
            _back.OnClick(() => _choice.Set(null));
            _play.OnClick(() =>
            {
                if (_selected != null)
                    _choice.Set(_selected);
            });
        }

        public void Setup(ICampaignProgress progress, GameConfig config, ILoadout loadout, List<DieConfig> ownedDice)
        {
            _progress = progress;
            _config = config;
            if (_opponentTemplate != null)
                StarsText.Prepare(_opponentTemplate.Label, config.Ui);
            StarsText.Prepare(_info, config.Ui);
            _back.SetText(T("ui.back"));
            _myDiceLabel.text = T("campaign.myDice");
            ShowDice(loadout, config, ownedDice);
            BuildStakes();
            if (_presets != null)
                _presets.Setup(Localization, loadout, () => ShowDice(loadout, config, ownedDice));

            var current = progress.CurrentChapter;
            var index = 0;
            for (var i = 0; i < progress.Chapters.Count; i++)
            {
                if (progress.Chapters[i] == current)
                    index = i;
            }

            ShowChapter(index);
        }

        private void ShowDice(ILoadout loadout, GameConfig config, List<DieConfig> ownedDice)
        {
            _myDice.Setup(Localization, ownedDice, loadout.GetDice(), config.MaxSpecialDice, loadout.SetDie,
                _mastery != null ? _mastery.GetLevel : (System.Func<DieConfig, int>)null, config.MasteryLevels);
        }

        private void ShowConditions(OpponentConfig opponent, int mask)
        {
            foreach (Transform child in _conditionsList)
            {
                if (child != _conditionRowPrefab.transform)
                    Destroy(child.gameObject);
            }

            if (_conditionsTitle != null)
                _conditionsTitle.text = T("campaign.starsTitle");

            AddCondition((mask & 1) != 0, T("star.win"));
            for (var i = 0; i < opponent.StarConditions.Count; i++)
                AddCondition((mask & (1 << (i + 1))) != 0, StarsText.Condition(opponent.StarConditions[i], T));
        }

        private void AddCondition(bool got, string text)
        {
            var row = Instantiate(_conditionRowPrefab, _conditionsList);
            row.gameObject.SetActive(true);
            row.Setup(got, text, _config.Ui);
        }

        /// <summary>Награды одной строкой: «90 монет, Пятёрочная». Предмет, который уже есть, помечается.</summary>
        private string RewardsText(IReadOnlyList<Reward> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            foreach (var reward in rewards)
            {
                switch (reward)
                {
                    case CurrencyReward currency when currency.Currency != null && currency.Amount > 0:
                        parts.Add(currency.Amount + " " + T(currency.Currency.NameKey));
                        break;
                    case ContentReward content when content.Item != null:
                        var owned = _inventory != null && _inventory.IsOwned(content.Item);
                        parts.Add(RewardNames.Describe(content.Item, T) + (owned ? " " + T("campaign.rewardOwned") : string.Empty));
                        break;
                }
            }

            return string.Join(", ", parts);
        }

        private static string StarLine(int mask, int bit, string condition)
        {
            var got = (mask & (1 << bit)) != 0;
            var color = ColorUtility.ToHtmlStringRGB(got ? UiColors.Gold : UiColors.TextMuted);
            return "<color=#" + color + ">" + StarsText.Star + " " + condition + "</color>";
        }

        /// <summary>Сколько чистыми получит игрок за победу при ставке stake (StakePayout соперника).</summary>
        public static int StakeWin(OpponentConfig opponent, int stake)
        {
            return opponent != null ? Mathf.FloorToInt(stake * Mathf.Max(0f, opponent.StakePayout)) : 0;
        }

        /// <summary>Кнопки ставок из GameConfig.StakeOptions; ставка дороже кошелька недоступна.</summary>
        private void BuildStakes()
        {
            if (_stakeList == null || _stakeTemplate == null)
                return;

            _stakeTemplate.gameObject.SetActive(false);
            if (_stakeLabel != null)
                _stakeLabel.text = T("campaign.stake");

            foreach (var button in _stakeButtons)
                Destroy(button.gameObject);
            _stakeButtons.Clear();

            var options = _config.StakeOptions ?? new int[0];
            _stakeList.gameObject.SetActive(options.Length > 1 && _config.Coins != null);
            foreach (var option in options)
            {
                var amount = option;
                var button = _stakeTemplate.Spawn(_stakeList);
                button.SetText(amount > 0 ? amount.ToString() : T("campaign.noStake"));
                button.OnClick(() => SelectStake(amount));
                _stakeButtons.Add(button);
            }

            SelectStake(0);
        }

        private void SelectStake(int amount)
        {
            var coins = _config.Coins != null ? _wallet.Get(_config.Coins) : 0;
            Stake = amount <= coins ? amount : 0;
            var options = _config.StakeOptions ?? new int[0];
            for (var i = 0; i < _stakeButtons.Count && i < options.Length; i++)
            {
                _stakeButtons[i].Interactable = options[i] <= coins;
                _stakeButtons[i].SetColor(options[i] == Stake ? UiColors.ButtonAccent : UiColors.ButtonMuted);
            }

            if (_selected != null)
                Select(_selected);
        }

        public UniTask<OpponentConfig> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }

        /// <summary>Сколько энергии стоит партия с соперником.</summary>
        public static int EnergyCost(OpponentConfig opponent, GameConfig config)
        {
            if (opponent != null && opponent.EnergyCost >= 0)
                return opponent.EnergyCost;
            return config.CampaignMode != null ? config.CampaignMode.EnergyCost : 0;
        }

        private void ShowChapter(int index)
        {
            if (_progress.Chapters.Count == 0)
            {
                _chapterTitle.text = T("campaign.empty");
                _play.Interactable = false;
                return;
            }

            _chapterIndex = Mathf.Clamp(index, 0, _progress.Chapters.Count - 1);
            Chapter = _progress.Chapters[_chapterIndex];
            _previousChapter.Interactable = _chapterIndex > 0;
            _nextChapter.Interactable = _chapterIndex < _progress.Chapters.Count - 1;

            var unlocked = _progress.IsChapterUnlocked(Chapter);
            _chapterTitle.text = T("campaign.chapter", _chapterIndex + 1, T(Chapter.NameKey)) +
                                 (unlocked ? string.Empty : "  " + T("campaign.locked"));

            foreach (Transform child in _opponentList)
            {
                if (_opponentTemplate == null || child != _opponentTemplate.transform)
                    Destroy(child.gameObject);
            }

            OpponentConfig firstAvailable = null;
            foreach (var opponent in Chapter.Opponents)
            {
                if (opponent == null)
                    continue;

                var state = _progress.GetState(Chapter, opponent);
                var captured = opponent;
                var rowColor = state == OpponentState.Beaten ? UiColors.ButtonMuted
                    : state == OpponentState.Available ? (opponent.IsBoss ? UiColors.ButtonAccent : UiColors.Button)
                    : new Color(0.2f, 0.2f, 0.2f, 1f);

                if (firstAvailable == null && state == OpponentState.Available)
                    firstAvailable = opponent;

                // Строка из префаба: вид (звёзды картинками, портрет, шрифты) настраивается в Parts/OpponentRow.
                if (_opponentRowPrefab != null)
                {
                    var row = Instantiate(_opponentRowPrefab, _opponentList);
                    row.gameObject.SetActive(true);
                    var rowStatus = (opponent.IsBoss ? T("campaign.boss") : string.Empty) +
                                    (state == OpponentState.Locked ? " " + T("campaign.state.locked") : string.Empty);
                    row.Setup(T(opponent.NameKey), rowStatus.Trim(), opponent.Portrait, _progress.GetStarMask(opponent),
                        StarsText.MaxStars(opponent), _config.Ui, rowColor, state != OpponentState.Locked, () => Select(captured));
                    continue;
                }

                var button = _opponentTemplate.Spawn(_opponentList);
                // Имя, у босса пометка, звёзды у всех (серые — ещё не получены), у закрытого — «закрыт».
                var stars = StarsText.Render(_progress.GetStarMask(opponent), StarsText.MaxStars(opponent));
                var status = state == OpponentState.Locked ? stars + "  " + T("campaign.state.locked") : stars;
                button.SetText(T(opponent.NameKey) + (opponent.IsBoss ? "  " + T("campaign.boss") : string.Empty) + "  " + status);
                button.SetColor(rowColor);
                button.Interactable = state != OpponentState.Locked;
                button.OnClick(() => Select(captured));
            }

            Select(firstAvailable);
        }

        private void Select(OpponentConfig opponent)
        {
            _selected = opponent;
            if (opponent == null)
            {
                if (_portrait != null)
                    _portrait.gameObject.SetActive(false);
                _info.text = T("campaign.pick");
                _play.SetText(T("campaign.play"));
                _play.Interactable = false;
                return;
            }

            if (_portrait != null)
            {
                _portrait.sprite = opponent.Portrait;
                _portrait.gameObject.SetActive(opponent.Portrait != null);
            }

            var text = $"<b>{T(opponent.NameKey)}</b>\n{T(opponent.TitleKey)}\n\n";
            if (!string.IsNullOrEmpty(opponent.RuleKey))
                text += T("campaign.rule") + "\n" + T(opponent.RuleKey) + "\n\n";

            var target = opponent.TargetScore > 0 ? opponent.TargetScore
                : _config.CampaignMode != null && _config.CampaignMode.Rules != null ? _config.CampaignMode.Rules.TargetScore : 4000;
            text += T("campaign.target", target);

            // Звёзды: что нужно для каждой; полученные — золотые.
            var mask = _progress.GetStarMask(opponent);
            text += "\n\n" + T("campaign.starsTitle");
            text += "\n" + StarLine(mask, 0, T("star.win"));
            for (var i = 0; i < opponent.StarConditions.Count; i++)
                text += "\n" + StarLine(mask, i + 1, StarsText.Condition(opponent.StarConditions[i], T));

            // Награда за победу: первая — полная, повторная — меньше (из конфига соперника), плюс монеты за новые звёзды.
            var beaten = _progress.GetState(Chapter, opponent) == OpponentState.Beaten;
            var rewards = RewardsText(beaten ? opponent.RepeatWinRewards : opponent.FirstWinRewards);
            if (rewards.Length > 0)
                text += "\n\n" + T(beaten ? "campaign.rewardRepeat" : "campaign.rewardFirst", rewards);
            var starReward = RewardsText(_config.NewStarRewards);
            if (starReward.Length > 0 && StarsText.CountBits(mask) < StarsText.MaxStars(opponent))
                text += "\n" + T("campaign.rewardStar", starReward);

            if (Stake > 0)
                text += "\n\n" + T("campaign.stakeInfo", Stake, Stake + StakeWin(opponent, Stake));

            _info.text = text;

            var cost = EnergyCost(opponent, _config);
            _play.SetText(cost > 0 ? T("campaign.playCost", cost) : T("campaign.play"));
            _play.Interactable = true;
        }
    }
}
