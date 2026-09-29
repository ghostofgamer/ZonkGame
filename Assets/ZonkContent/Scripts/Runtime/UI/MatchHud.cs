using TMPro;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zonk.Configs;
using Zonk.Core.Match;
using Zonk.MatchFlow;

namespace Zonk.UI
{
    public enum HudAction
    {
        Roll,
        Bank,
    }

    /// <summary>
    /// Интерфейс партии: счёт игроков, очки хода, кнопки «Бросить» и «Забрать», быстрые фразы, сдаться.
    /// Кнопки ждут решения местного игрока через UniTask; ИИ и сеть HUD не трогают.
    /// </summary>
    public sealed class MatchHud : UiScreen
    {
        private readonly TMP_Text[] _names = new TMP_Text[2];
        private readonly TMP_Text[] _scores = new TMP_Text[2];
        private readonly Image[] _panels = new Image[2];
        private readonly TMP_Text _turn;
        private readonly TMP_Text _hint;
        private readonly UiButton _roll;
        private readonly UiButton _bank;
        private readonly RectTransform _phrasePanel;
        private readonly Choice<HudAction> _action = new Choice<HudAction>();
        private Func<(bool canRoll, bool canBank, int bankScore)> _actionState;

        public event Action<PhraseConfig> PhraseChosen;
        public event Action SurrenderRequested;

        public MatchHud(UiKit kit, RectTransform parent, IReadOnlyList<PhraseConfig> phrases) : base(kit, parent)
        {
            for (var i = 0; i < 2; i++)
            {
                var anchor = i == 0 ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                var panel = kit.Panel("Player" + i, Root, UiColors.Panel);
                UiKit.Place(panel.rectTransform, anchor, new Vector2(420, 120), new Vector2(i == 0 ? 24 : -24, -24));
                var column = kit.Column(panel.transform, 2, 10);
                UiKit.Stretch((RectTransform)column.transform);
                _names[i] = kit.Label(column.transform, string.Empty, 28, TextAnchor.MiddleCenter);
                _scores[i] = kit.Label(column.transform, string.Empty, 40, TextAnchor.MiddleCenter, UiColors.Gold);
                _panels[i] = panel;
            }

            _turn = kit.Label(Root, string.Empty, 34, TextAnchor.MiddleCenter);
            UiKit.Place(_turn.rectTransform, new Vector2(0.5f, 1f), new Vector2(700, 110), new Vector2(0, -20));

            _hint = kit.Label(Root, string.Empty, 30, TextAnchor.MiddleCenter, UiColors.Text);
            UiKit.Outline(_hint, new Color(0f, 0f, 0f, 0.8f));
            UiKit.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(900, 60), new Vector2(0, 130));

            var buttons = kit.Row(Root, 24);
            UiKit.Place((RectTransform)buttons.transform, new Vector2(0.5f, 0f), new Vector2(760, 96), new Vector2(0, 24));
            _roll = kit.Button(buttons.transform, kit.T("match.roll"), () => _action.Set(HudAction.Roll), UiColors.ButtonAccent, 34);
            _bank = kit.Button(buttons.transform, kit.T("match.bank"), () => _action.Set(HudAction.Bank), UiColors.Button, 30);
            HideButtons();

            var menu = kit.Button(Root, kit.T("match.surrender"), () => SurrenderRequested?.Invoke(), UiColors.ButtonMuted, 22);
            UiKit.Place((RectTransform)menu.GameObject.transform, new Vector2(1f, 0f), new Vector2(200, 64), new Vector2(-24, 24));

            var phraseButton = kit.Button(Root, kit.T("match.phrases"), TogglePhrases, UiColors.ButtonMuted, 22);
            UiKit.Place((RectTransform)phraseButton.GameObject.transform, new Vector2(0f, 0f), new Vector2(200, 64), new Vector2(24, 24));

            var phrasePanel = kit.Panel("Phrases", Root, UiColors.Panel);
            _phrasePanel = phrasePanel.rectTransform;
            UiKit.Place(_phrasePanel, new Vector2(0f, 0f), new Vector2(380, 80 + phrases.Count * 70), new Vector2(24, 100));
            var phraseColumn = kit.Column(phrasePanel.transform, 8, 10);
            UiKit.Stretch((RectTransform)phraseColumn.transform);
            foreach (var phrase in phrases)
            {
                var captured = phrase;
                kit.Button(phraseColumn.transform, kit.T(phrase.NameKey), () =>
                {
                    _phrasePanel.gameObject.SetActive(false);
                    PhraseChosen?.Invoke(captured);
                }, UiColors.Button, 22);
            }

            _phrasePanel.gameObject.SetActive(false);
        }

        public RectTransform CanvasRoot => (RectTransform)Root.parent;

        public void SetParticipants(IReadOnlyList<MatchParticipant> participants)
        {
            for (var i = 0; i < 2 && i < participants.Count; i++)
            {
                _names[i].text = participants[i].Name;
                _names[i].color = participants[i].Color;
            }
        }

        public void Refresh(ZonkMatch match)
        {
            for (var i = 0; i < 2 && i < match.Players.Count; i++)
            {
                _scores[i].text = match.Players[i].Score.ToString();
                var active = match.Phase != MatchPhase.Finished && match.CurrentPlayerIndex == i;
                _panels[i].color = active ? UiColors.PanelLight : UiColors.Panel;
                _panels[i].rectTransform.localScale = active ? Vector3.one * 1.06f : Vector3.one;
            }

            var target = Kit.T("match.target", match.Rules.TargetScore);
            if (match.IsFinalRound)
                target += "\n" + Kit.T("match.finalRound");
            else if (match.Phase != MatchPhase.Finished)
                target += "\n" + Kit.T("match.turnScore", match.TurnScore);
            _turn.text = target;
        }

        public void SetHint(string text)
        {
            _hint.text = text;
        }

        /// <summary>Начало хода: только «Бросить».</summary>
        public async UniTask WaitRollAsync(CancellationToken ct)
        {
            _roll.SetVisible(true);
            _roll.Interactable = true;
            _bank.SetVisible(false);
            try
            {
                while (await _action.WaitAsync(ct) != HudAction.Roll)
                {
                }
            }
            finally
            {
                HideButtons();
            }
        }

        /// <summary>
        /// После броска: игрок выбирает кости, кнопки включаются по мере выбора.
        /// state сообщает, что сейчас можно; при смене выбора вызывать RefreshActions.
        /// </summary>
        public async UniTask<HudAction> WaitActionAsync(Func<(bool canRoll, bool canBank, int bankScore)> state,
            CancellationToken ct)
        {
            _actionState = state;
            _roll.SetVisible(true);
            _bank.SetVisible(true);
            RefreshActions();

            try
            {
                while (true)
                {
                    var action = await _action.WaitAsync(ct);
                    var (canRoll, canBank, _) = state();
                    if ((action == HudAction.Roll && canRoll) || (action == HudAction.Bank && canBank))
                        return action;
                }
            }
            finally
            {
                _actionState = null;
                HideButtons();
            }
        }

        public void RefreshActions()
        {
            if (_actionState == null)
                return;

            var (canRoll, canBank, bankScore) = _actionState();
            _roll.Interactable = canRoll;
            _bank.Interactable = canBank;
            _bank.SetText(Kit.T("match.bankScore", bankScore));
        }

        public void HideButtons()
        {
            _roll.SetVisible(false);
            _bank.SetVisible(false);
        }

        public UniTask ToastAsync(string text, Color color, float duration, CancellationToken ct)
        {
            return Toast.ShowAsync(Kit, Root, text, color, duration, ct);
        }

        private void TogglePhrases()
        {
            _phrasePanel.gameObject.SetActive(!_phrasePanel.gameObject.activeSelf);
        }
    }
}
