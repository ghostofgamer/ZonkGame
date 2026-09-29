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
        [SerializeField] private TMP_Text _info;
        [SerializeField] private TMP_Text _myDiceLabel;
        [SerializeField] private DiceLoadoutView _myDice;
        [SerializeField] private UiButtonView _back;
        [SerializeField] private UiButtonView _play;

        private readonly Choice<OpponentConfig> _choice = new Choice<OpponentConfig>();
        private ICampaignProgress _progress;
        private GameConfig _config;
        private int _chapterIndex;
        private OpponentConfig _selected;

        public ChapterConfig Chapter { get; private set; }

#if UNITY_EDITOR
        public void EditorSetup(UiButtonView previousChapter, UiButtonView nextChapter, TMP_Text chapterTitle,
            RectTransform opponentList, UiButtonView opponentTemplate, TMP_Text info, TMP_Text myDiceLabel, DiceLoadoutView myDice,
            UiButtonView back, UiButtonView play)
        {
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
            _back.SetText(T("ui.back"));
            _myDiceLabel.text = T("campaign.myDice");
            _myDice.Setup(Localization, ownedDice, loadout.GetDice(), config.MaxSpecialDice, loadout.SetDie);

            var current = progress.CurrentChapter;
            var index = 0;
            for (var i = 0; i < progress.Chapters.Count; i++)
            {
                if (progress.Chapters[i] == current)
                    index = i;
            }

            ShowChapter(index);
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
                if (child != _opponentTemplate.transform)
                    Destroy(child.gameObject);
            }

            OpponentConfig firstAvailable = null;
            foreach (var opponent in Chapter.Opponents)
            {
                if (opponent == null)
                    continue;

                var state = _progress.GetState(Chapter, opponent);
                var button = _opponentTemplate.Spawn(_opponentList);
                button.SetText(T(opponent.NameKey) + (opponent.IsBoss ? "  ★" : string.Empty) + "  —  " +
                               T("campaign.state." + state.ToString().ToLowerInvariant()));
                button.SetColor(state == OpponentState.Beaten ? UiColors.ButtonMuted
                    : state == OpponentState.Available ? (opponent.IsBoss ? UiColors.ButtonAccent : UiColors.Button)
                    : new Color(0.2f, 0.2f, 0.2f, 1f));
                button.Interactable = state != OpponentState.Locked;
                var captured = opponent;
                button.OnClick(() => Select(captured));

                if (firstAvailable == null && state == OpponentState.Available)
                    firstAvailable = opponent;
            }

            Select(firstAvailable);
        }

        private void Select(OpponentConfig opponent)
        {
            _selected = opponent;
            if (opponent == null)
            {
                _info.text = T("campaign.pick");
                _play.SetText(T("campaign.play"));
                _play.Interactable = false;
                return;
            }

            var text = $"<b>{T(opponent.NameKey)}</b>\n{T(opponent.TitleKey)}\n\n";
            if (!string.IsNullOrEmpty(opponent.RuleKey))
                text += T("campaign.rule") + "\n" + T(opponent.RuleKey) + "\n\n";

            var target = opponent.TargetScore > 0 ? opponent.TargetScore
                : _config.CampaignMode != null && _config.CampaignMode.Rules != null ? _config.CampaignMode.Rules.TargetScore : 4000;
            _info.text = text + T("campaign.target", target);

            var cost = EnergyCost(opponent, _config);
            _play.SetText(cost > 0 ? T("campaign.playCost", cost) : T("campaign.play"));
            _play.Interactable = true;
        }
    }
}
