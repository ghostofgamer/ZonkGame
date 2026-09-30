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
using Zonk.Utils;

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
        private MatchFeelConfig _feel;

        // Строка цели и очков хода: префикс и суффикс вокруг числа, само число накручивается без сборки строк.
        private int _headTarget = -1;
        private int _headMode = -1;
        private string _turnPrefix;
        private string _turnSuffix;
        private int _turnShown;
        private int _turnTarget;
        private int _turnFrom;
        private float _countTime = -1f;
        private float _punchTime = -1f;

        public event Action<PhraseConfig> PhraseChosen;
        public event Action SurrenderRequested;

        [Inject]
        public void Construct(UiKit kit, GameConfig config)
        {
            _kit = kit;
            _feel = config != null && config.Feel != null ? config.Feel : MatchFeelConfig.Fallback;
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
            {
                _players[i].SetCounting(_feel.BankCountDuration, _feel.CountPunch, _feel.CountPunchDuration);
                _players[i].SetPlayer(participants[i].Name, participants[i].Color);
                _players[i].SetPortrait(participants[i].Opponent != null ? participants[i].Opponent.Portrait : null);
            }
        }

        public void Refresh(ZonkMatch match)
        {
            for (var i = 0; i < _players.Length && i < match.Players.Count; i++)
                _players[i].SetScore(match.Players[i].Score, match.Phase != MatchPhase.Finished && match.CurrentPlayerIndex == i);

            // Режим строки: 0 — очки хода, 1 — последний круг, 2 — партия окончена. Строки собираются при смене режима.
            var mode = match.IsFinalRound ? 1 : match.Phase == MatchPhase.Finished ? 2 : 0;
            if (mode != _headMode || match.Rules.TargetScore != _headTarget)
            {
                _headMode = mode;
                _headTarget = match.Rules.TargetScore;
                var head = T("match.target", match.Rules.TargetScore);
                _countTime = -1f;
                if (mode == 1)
                    _turn.text = head + "\n" + T("match.finalRound");
                else if (mode == 2)
                    _turn.text = head;
                else
                {
                    var template = T("match.turnScore");
                    var at = template.IndexOf("{0}", StringComparison.Ordinal);
                    _turnPrefix = head + "\n" + (at >= 0 ? template.Substring(0, at) : template + " ");
                    _turnSuffix = at >= 0 ? template.Substring(at + 3) : null;
                    _turnShown = _turnTarget = match.TurnScore;
                    NumberText.Set(_turn, _turnPrefix, _turnShown, _turnSuffix);
                }
            }

            if (mode == 0)
                SetTurnScore(match.TurnScore);
        }

        /// <summary>Очки хода: рост накручивается с подпрыгиванием, сброс (новый ход, Зонк) — сразу.</summary>
        private void SetTurnScore(int score)
        {
            if (score == _turnTarget)
                return;

            _turnTarget = score;
            if (score > _turnShown && _feel.TurnCountDuration > 0f && isActiveAndEnabled)
            {
                _turnFrom = _turnShown;
                _countTime = 0f;
                _punchTime = 0f;
                return;
            }

            _countTime = -1f;
            _turnShown = score;
            NumberText.Set(_turn, _turnPrefix, _turnShown, _turnSuffix);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_countTime >= 0f)
            {
                _countTime += dt / _feel.TurnCountDuration;
                var t = Mathf.Clamp01(_countTime);
                var value = Mathf.RoundToInt(Mathf.Lerp(_turnFrom, _turnTarget, 1f - (1f - t) * (1f - t)));
                if (value != _turnShown)
                {
                    _turnShown = value;
                    NumberText.Set(_turn, _turnPrefix, _turnShown, _turnSuffix);
                }

                if (t >= 1f)
                    _countTime = -1f;
            }

            if (_punchTime >= 0f)
            {
                _punchTime += dt / Mathf.Max(0.01f, _feel.CountPunchDuration);
                var p = Mathf.Clamp01(_punchTime);
                _turn.rectTransform.localScale = Vector3.one * (1f + (_feel.CountPunch - 1f) * Mathf.Sin(p * Mathf.PI));
                if (p >= 1f)
                    _punchTime = -1f;
            }
        }

        /// <summary>
        /// Очки отложенных костей всплывают над ними: «+350» и название комбинации под ним. Крупная комбинация —
        /// крупнее и золотом. Надписи из пула UiKit, живут PopupDuration секунд.
        /// </summary>
        public void ShowKeepScore(Vector3 world, Camera camera, int score, string comboKey, bool big)
        {
            if (!isActiveAndEnabled || camera == null || score <= 0)
                return;

            PopupAsync(world, camera, score, string.IsNullOrEmpty(comboKey) ? null : T(comboKey), big,
                this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid PopupAsync(Vector3 world, Camera camera, int score, string combo, bool big,
            CancellationToken ct)
        {
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
                return;

            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out var local))
                return;

            // Точка относительно центра окна, не ближе к краю, чем полнадписи: на узком экране не вылезает.
            var size = rect.rect.size;
            var anchored = local + (rect.pivot - new Vector2(0.5f, 0.5f)) * size;
            anchored.x = Mathf.Clamp(anchored.x, -size.x * 0.5f + 360f, size.x * 0.5f - 360f);
            anchored.y = Mathf.Clamp(anchored.y, -size.y * 0.5f + 160f, size.y * 0.5f - 260f);

            var scale = big ? 1.3f : 1f;
            var color = big ? UiColors.Gold : UiColors.Text;
            var points = _kit.RentPopup(rect, _feel.PopupFontSize * scale);
            var name = combo != null ? _kit.RentPopup(rect, _feel.ComboFontSize * scale) : null;
            NumberText.Set(points, "+", score);
            if (name != null)
                name.text = combo;

            try
            {
                var duration = Mathf.Max(0.3f, _feel.PopupDuration);
                var time = 0f;
                while (time < duration)
                {
                    var t = time / duration;
                    var pop = t < 0.18f ? AnimateEase.OutBack(t / 0.18f) : 1f;
                    var alpha = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                    var offset = new Vector2(0f, _feel.PopupRise * (1f - (1f - t) * (1f - t)));
                    Place(points, anchored + offset, pop, color, alpha);
                    if (name != null)
                        Place(name, anchored + offset + new Vector2(0f, -_feel.PopupFontSize * scale), pop, UiColors.Gold, alpha);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    time += Time.deltaTime;
                }
            }
            finally
            {
                if (points != null)
                    _kit.ReturnPopup(points);
                if (name != null)
                    _kit.ReturnPopup(name);
            }
        }

        private static void Place(TMP_Text label, Vector2 position, float scale, Color color, float alpha)
        {
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.localScale = new Vector3(scale, scale, 1f);
            color.a = alpha;
            label.color = color;
        }

        public void SetHint(string text)
        {
            _hint.text = text;
        }

        /// <summary>Перед каждым броском: только «Бросить» (стакан можно взять и рукой — MatchPresenter.WaitGrabAsync).</summary>
        public async UniTask WaitRollAsync(CancellationToken ct)
        {
            _roll.SetText(T("match.roll"));
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

        /// <summary>После броска: «Продолжить» и «Забрать» включаются по мере выбора костей; при смене выбора вызывать RefreshActions.</summary>
        public async UniTask<HudAction> WaitActionAsync(Func<(bool canRoll, bool canBank, int bankScore)> state,
            CancellationToken ct)
        {
            _actionState = state;
            // «Продолжить» — только решение рискнуть дальше: бросок следующим шагом, кнопкой «Бросить» или стаканом.
            _roll.SetText(T("match.continue"));
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
