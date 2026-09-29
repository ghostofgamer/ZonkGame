using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Core.Match;
using Zonk.MatchFlow;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    public enum HudAction
    {
        Roll,
        Bank,
    }

    /// <summary>
    /// Интерфейс партии: таблички игроков в верхних углах, цель и очки хода сверху по центру, подсказка и кнопки
    /// «Бросить» / «Забрать» снизу, «Фразы» и «Сдаться» в нижних углах. Кнопки ждут решения местного игрока;
    /// ИИ и сеть HUD не трогают.
    /// </summary>
    public sealed class MatchHudWindow : UiWindow
    {
        [SerializeField] private HudPlayerPanel[] _players = new HudPlayerPanel[2];
        [SerializeField] private TMP_Text _turn;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private UiButtonView _roll;
        [SerializeField] private UiButtonView _bank;
        [SerializeField] private UiButtonView _surrender;
        [SerializeField] private UiButtonView _phrasesButton;
        [SerializeField] private RectTransform _phrasesPanel;
        [SerializeField] private UiButtonView _phraseTemplate;

        private readonly Choice<HudAction> _action = new Choice<HudAction>();
        private Func<(bool canRoll, bool canBank, int bankScore)> _actionState;
        private UiKit _kit;

        public event Action<PhraseConfig> PhraseChosen;
        public event Action SurrenderRequested;

        [Inject]
        public void Construct(UiKit kit)
        {
            _kit = kit;
        }

#if UNITY_EDITOR
        public void EditorSetup(HudPlayerPanel[] players, TMP_Text turn, TMP_Text hint, UiButtonView roll, UiButtonView bank,
            UiButtonView surrender, UiButtonView phrasesButton, RectTransform phrasesPanel, UiButtonView phraseTemplate)
        {
            _players = players;
            _turn = turn;
            _hint = hint;
            _roll = roll;
            _bank = bank;
            _surrender = surrender;
            _phrasesButton = phrasesButton;
            _phrasesPanel = phrasesPanel;
            _phraseTemplate = phraseTemplate;
        }
#endif

        /// <summary>Холст, в котором окно лежит: для облачков реплик и надписей поверх стола.</summary>
        public RectTransform CanvasRoot => (RectTransform)transform.parent;

        private void Awake()
        {
            _phraseTemplate.gameObject.SetActive(false);
            _phrasesPanel.gameObject.SetActive(false);
            _roll.OnClick(() => _action.Set(HudAction.Roll));
            _bank.OnClick(() => _action.Set(HudAction.Bank));
            _surrender.OnClick(() => SurrenderRequested?.Invoke());
            _phrasesButton.OnClick(() => _phrasesPanel.gameObject.SetActive(!_phrasesPanel.gameObject.activeSelf));
            HideButtons();
        }

        public void Setup(IReadOnlyList<PhraseConfig> phrases, IReadOnlyList<MatchParticipant> participants)
        {
            // Цвета главных кнопок из палитры UiConfig: «Бросить» и «Забрать» явно разные.
            _roll.SetColor(UiColors.ButtonAccent);
            _bank.SetColor(UiColors.Bank);
            _roll.SetText(T("match.roll"));
            _bank.SetText(T("match.bank"));
            _surrender.SetText(T("match.surrender"));
            _phrasesButton.SetText(T("match.phrases"));

            foreach (var phrase in phrases)
            {
                var captured = phrase;
                var button = _phraseTemplate.Spawn(_phrasesPanel);
                button.SetText(T(phrase.NameKey));
                button.OnClick(() =>
                {
                    _phrasesPanel.gameObject.SetActive(false);
                    PhraseChosen?.Invoke(captured);
                });
            }

            for (var i = 0; i < _players.Length && i < participants.Count; i++)
                _players[i].SetPlayer(participants[i].Name, participants[i].Color);
        }

        public void Refresh(ZonkMatch match)
        {
            for (var i = 0; i < _players.Length && i < match.Players.Count; i++)
                _players[i].SetScore(match.Players[i].Score, match.Phase != MatchPhase.Finished && match.CurrentPlayerIndex == i);

            var text = T("match.target", match.Rules.TargetScore);
            if (match.IsFinalRound)
                text += "\n" + T("match.finalRound");
            else if (match.Phase != MatchPhase.Finished)
                text += "\n" + T("match.turnScore", match.TurnScore);
            _turn.text = text;
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

        /// <summary>После броска: кнопки включаются по мере выбора костей; при смене выбора вызывать RefreshActions.</summary>
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
            _bank.SetText(T("match.bankScore", bankScore));
        }

        public void HideButtons()
        {
            _roll.SetVisible(false);
            _bank.SetVisible(false);
        }

        public UniTask ToastAsync(string text, Color color, float duration, CancellationToken ct)
        {
            return Toast.ShowAsync(_kit, (RectTransform)transform, text, color, duration, ct);
        }
    }
}
