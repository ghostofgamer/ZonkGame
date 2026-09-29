using TMPro;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.UI
{
    /// <summary>Выбор в кампании: соперник для партии или null (назад).</summary>
    public sealed class CampaignScreen : UiScreen
    {
        private readonly Choice<OpponentConfig> _choice = new Choice<OpponentConfig>();
        private readonly ICampaignProgress _progress;
        private readonly IWallet _wallet;
        private readonly GameConfig _config;
        private readonly RectTransform _list;
        private readonly TMP_Text _chapterTitle;
        private readonly TMP_Text _info;
        private readonly UiButton _play;
        private int _chapterIndex;
        private OpponentConfig _selected;
        private ChapterConfig _chapter;

        public CampaignScreen(UiKit kit, RectTransform parent, ICampaignProgress progress, IWallet wallet, GameConfig config,
            ContentDatabase content, IInventory inventory, ILoadout loadout, List<DieConfig> ownedDice) : base(kit, parent)
        {
            _progress = progress;
            _wallet = wallet;
            _config = config;

            kit.Blocker(Root, 0.35f);
            var panel = kit.Panel("Campaign", Root, UiColors.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1560, 920));
            var column = kit.Column(panel.transform, 12, 24);
            UiKit.Stretch((RectTransform)column.transform);

            var header = kit.Row(column.transform, 16);
            UiKit.Size(header, -1, 72);
            UiKit.Size(kit.Button(header.transform, "◀", () => ShowChapter(_chapterIndex - 1), UiColors.ButtonMuted, 36).Background, 90);
            _chapterTitle = kit.Label(header.transform, string.Empty, 38, TextAnchor.MiddleCenter, UiColors.Gold);
            UiKit.Size(kit.Button(header.transform, "▶", () => ShowChapter(_chapterIndex + 1), UiColors.ButtonMuted, 36).Background, 90);

            var body = kit.Row(column.transform, 20);
            UiKit.Size(body, -1, 560);
            var listColumn = kit.Column(body.transform, 8, 0);
            _list = (RectTransform)listColumn.transform;
            var infoPanel = kit.Panel("Info", body.transform, UiColors.PanelLight);
            _info = kit.Label(infoPanel.transform, string.Empty, 26, TextAnchor.UpperLeft);
            UiKit.Stretch(_info.rectTransform, 20, 20, 20, 20);

            UiKit.Size(kit.Label(column.transform, kit.T("campaign.myDice"), 24, TextAnchor.MiddleLeft, UiColors.TextMuted), -1, 34);
            new DiceLoadoutRow(kit, column.transform, ownedDice, loadout.GetDice(), config.MaxSpecialDice, loadout.SetDie);

            var buttons = kit.Row(column.transform, 24);
            UiKit.Size(buttons, -1, 84);
            kit.Button(buttons.transform, kit.T("ui.back"), () => _choice.Set(null), UiColors.ButtonMuted);
            _play = kit.Button(buttons.transform, string.Empty, () =>
            {
                if (_selected != null)
                    _choice.Set(_selected);
            }, UiColors.ButtonAccent, 32);

            WalletBar.Create(kit, Root, wallet, config);

            var current = progress.CurrentChapter;
            ShowChapter(current != null ? IndexOf(current) : 0);
        }

        public UniTask<OpponentConfig> RunAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }

        public ChapterConfig Chapter => _chapter;

        /// <summary>Сколько энергии стоит партия с соперником.</summary>
        public static int EnergyCost(OpponentConfig opponent, GameConfig config)
        {
            if (opponent != null && opponent.EnergyCost >= 0)
                return opponent.EnergyCost;
            return config.CampaignMode != null ? config.CampaignMode.EnergyCost : 0;
        }

        private int IndexOf(ChapterConfig chapter)
        {
            for (var i = 0; i < _progress.Chapters.Count; i++)
            {
                if (_progress.Chapters[i] == chapter)
                    return i;
            }

            return 0;
        }

        private void ShowChapter(int index)
        {
            if (_progress.Chapters.Count == 0)
            {
                _chapterTitle.text = Kit.T("campaign.empty");
                _play.Interactable = false;
                return;
            }

            _chapterIndex = Mathf.Clamp(index, 0, _progress.Chapters.Count - 1);
            _chapter = _progress.Chapters[_chapterIndex];
            var unlocked = _progress.IsChapterUnlocked(_chapter);
            _chapterTitle.text = Kit.T("campaign.chapter", _chapterIndex + 1, Kit.T(_chapter.NameKey)) +
                                 (unlocked ? string.Empty : "  " + Kit.T("campaign.locked"));

            foreach (Transform child in _list)
                Object.Destroy(child.gameObject);

            _selected = null;
            OpponentConfig firstAvailable = null;
            foreach (var opponent in _chapter.Opponents)
            {
                if (opponent == null)
                    continue;

                var state = _progress.GetState(_chapter, opponent);
                var label = Kit.T(opponent.NameKey) + (opponent.IsBoss ? "  ★" : string.Empty) + "  —  " +
                            Kit.T("campaign.state." + state.ToString().ToLowerInvariant());
                var color = state == OpponentState.Beaten ? UiColors.ButtonMuted
                    : state == OpponentState.Available ? (opponent.IsBoss ? UiColors.ButtonAccent : UiColors.Button)
                    : new Color(0.2f, 0.2f, 0.2f, 1f);
                var captured = opponent;
                var button = Kit.Button(_list, label, () => Select(captured), color, 24);
                button.Interactable = state != OpponentState.Locked;
                UiKit.Size(button.Background, -1, 64);

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
                _info.text = Kit.T("campaign.pick");
                _play.SetText(Kit.T("campaign.play"));
                _play.Interactable = false;
                return;
            }

            var text = $"<b>{Kit.T(opponent.NameKey)}</b>\n{Kit.T(opponent.TitleKey)}\n\n";
            if (!string.IsNullOrEmpty(opponent.RuleKey))
                text += Kit.T("campaign.rule") + "\n" + Kit.T(opponent.RuleKey) + "\n\n";

            var target = opponent.TargetScore > 0 ? opponent.TargetScore
                : _config.CampaignMode != null && _config.CampaignMode.Rules != null ? _config.CampaignMode.Rules.TargetScore : 4000;
            text += Kit.T("campaign.target", target);
            _info.richText = true;
            _info.text = text;

            var cost = EnergyCost(opponent, _config);
            _play.SetText(cost > 0 ? Kit.T("campaign.playCost", cost) : Kit.T("campaign.play"));
            _play.Interactable = true;
        }
    }

    /// <summary>Сюжетные карточки: говорящий и текст, «Дальше» и «Пропустить».</summary>
    public sealed class StoryScreen : UiScreen
    {
        private readonly Choice<bool> _next = new Choice<bool>();
        private readonly TMP_Text _speaker;
        private readonly TMP_Text _text;
        private bool _skip;

        public StoryScreen(UiKit kit, RectTransform parent) : base(kit, parent)
        {
            kit.Blocker(Root, 0.45f);
            var panel = kit.Panel("Story", Root, UiColors.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(1500, 360), new Vector2(0, 40));
            var column = kit.Column(panel.transform, 10, 28);
            UiKit.Stretch((RectTransform)column.transform);

            _speaker = kit.Label(column.transform, string.Empty, 32, TextAnchor.MiddleLeft, UiColors.Gold);
            UiKit.Size(_speaker, -1, 48);
            _text = kit.Label(column.transform, string.Empty, 30, TextAnchor.UpperLeft);
            UiKit.Size(_text, -1, 170);

            var buttons = kit.Row(column.transform, 20);
            UiKit.Size(buttons, -1, 64);
            kit.Button(buttons.transform, kit.T("story.skip"), () =>
            {
                _skip = true;
                _next.Set(true);
            }, UiColors.ButtonMuted, 24);
            kit.Button(buttons.transform, kit.T("story.next"), () => _next.Set(true), UiColors.ButtonAccent, 28);
        }

        public async UniTask PlayAsync(IReadOnlyList<StoryLine> lines, CancellationToken ct)
        {
            foreach (var line in lines)
            {
                _speaker.text = Kit.T(line.SpeakerKey);
                _text.text = Kit.T(line.TextKey);
                await _next.WaitAsync(ct);
                if (_skip)
                    return;
            }
        }
    }
}
