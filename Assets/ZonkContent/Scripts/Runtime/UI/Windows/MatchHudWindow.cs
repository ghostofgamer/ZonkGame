using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
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
        [FormerlySerializedAs("_surrender")]
        [SerializeField] private UiButtonView _pause;
        [SerializeField] private UiButtonView _phrasesButton;
        [SerializeField] private RectTransform _phrasesPanel;
        [SerializeField] private UiButtonView _phraseTemplate;

        [Header("Комикс (детали Parts; в старом префабе пусто — обычные надписи)")]
        [SerializeField] private ComicBurstView _burst;
        [SerializeField] private Image _turnPlate;
        [SerializeField] private GameObject _turnFire;
        [SerializeField] private GameObject _rollFire;
        [SerializeField] private GameObject _hintPlate;

        [Tooltip("Цвет таблички очков хода при горячих костях")]
        [SerializeField] private Color _turnPlateHot = new Color(1f, 0.776f, 0.18f, 1f);

        [Tooltip("Отступ таблички очков хода от верхнего угла экрана того, кто ходит")]
        [SerializeField] private Vector2 _turnPlateOffset = new Vector2(24f, -150f);

        private int _turnPlatePlayer = -1;
        private Color _turnPlateNormal;
        private bool _hot;

        private readonly Choice<HudAction> _action = new Choice<HudAction>();
        private Func<(bool canRoll, bool canBank, int bankScore)> _actionState;
        private UiKit _kit;
        private MatchFeelConfig _feel;

        // Строка цели и очков хода: префикс и суффикс вокруг числа, само число накручивается без сборки строк.
        private int _headTarget = -1;
        private int _headMode = -1;
        private string _turnPrefix;
        private string _turnSuffix;
        private readonly CountingNumber _turnScore = new CountingNumber();
        private readonly NumberTemplate _bankText = new NumberTemplate();

        public event Action<PhraseConfig> PhraseChosen;
        public event Action PauseRequested;

        [Inject]
        public void Construct(UiKit kit, GameConfig config)
        {
            _kit = kit;
            _feel = config != null && config.Feel != null ? config.Feel : MatchFeelConfig.Fallback;
            _turnScore.Configure(_feel.TurnCountDuration, _feel.CountPunch, _feel.CountPunchDuration);
        }

#if UNITY_EDITOR
        public void EditorSetup(HudPlayerPanel[] players, TMP_Text turn, TMP_Text hint, UiButtonView roll, UiButtonView bank,
            UiButtonView pause, UiButtonView phrasesButton, RectTransform phrasesPanel, UiButtonView phraseTemplate)
        {
            _players = players;
            _turn = turn;
            _hint = hint;
            _roll = roll;
            _bank = bank;
            _pause = pause;
            _phrasesButton = phrasesButton;
            _phrasesPanel = phrasesPanel;
            _phraseTemplate = phraseTemplate;
        }

        public void EditorSetupComic(ComicBurstView burst, Image turnPlate, GameObject turnFire, GameObject rollFire)
        {
            _burst = burst;
            _turnPlate = turnPlate;
            _turnFire = turnFire;
            _rollFire = rollFire;
        }

        public void EditorSetupHudLayout(GameObject hintPlate, Vector2 turnPlateOffset)
        {
            _hintPlate = hintPlate;
            _turnPlateOffset = turnPlateOffset;
        }
#endif

        private void Awake()
        {
            if (_turnPlate != null)
                _turnPlateNormal = _turnPlate.color;
            if (_burst != null)
                _burst.gameObject.SetActive(false);
            if (_turnFire != null)
                _turnFire.SetActive(false);
            if (_rollFire != null)
                _rollFire.SetActive(false);
            if (_hintPlate != null)
                _hintPlate.SetActive(false);
            _phraseTemplate.gameObject.SetActive(false);
            _phrasesPanel.gameObject.SetActive(false);
            _roll.OnClick(() => _action.Set(HudAction.Roll));
            _bank.OnClick(() => _action.Set(HudAction.Bank));
            _pause.OnClick(() => PauseRequested?.Invoke());
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
            _pause.SetText(T("match.pause"));
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

            PlaceTurnPlate(match.CurrentPlayerIndex);

            if (match.Rules.TargetScore != _headTarget)
            {
                _headTarget = match.Rules.TargetScore;
                for (var i = 0; i < _players.Length; i++)
                    _players[i].SetGoal(_headTarget);
            }

            // Подпись над числом: 0 — очки хода, 1 — последний круг (очки видны и в нём), 2 — партия окончена.
            // Строки собираются только при смене режима.
            var mode = match.Phase == MatchPhase.Finished ? 2 : match.IsFinalRound ? 1 : 0;
            if (mode != _headMode)
            {
                _headMode = mode;
                if (mode == 2)
                {
                    _turnScore.Jump(_turnScore.Target);
                    _turn.text = T("match.over");
                }
                else
                {
                    NumberTemplate.Split(T("match.turnScore"), out var prefix, out _turnSuffix);
                    var label = mode == 1 ? T("match.finalRound") : prefix.Trim().TrimEnd(':').Trim();
                    _turnPrefix = "<size=52%>" + label + "</size>\n";
                    _turnScore.Jump(match.TurnScore);
                    NumberText.Set(_turn, _turnPrefix, match.TurnScore, _turnSuffix);
                }
            }

            // Очки хода: рост накручивается с подпрыгиванием, сброс (новый ход, Зонк) — сразу.
            if (mode != 2 && _turnScore.SetTarget(match.TurnScore, isActiveAndEnabled))
                NumberText.Set(_turn, _turnPrefix, match.TurnScore, _turnSuffix);
        }

        /// <summary>Табличка очков хода — под табличкой того, кто ходит: верх экрана свободен для соперника и его аксессуаров.</summary>
        private void PlaceTurnPlate(int player)
        {
            if (_turnPlate == null || player == _turnPlatePlayer)
                return;

            _turnPlatePlayer = player;
            var rect = _turnPlate.rectTransform;
            var left = player == 0;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(left ? 0f : 1f, 1f);
            rect.anchoredPosition = new Vector2(left ? _turnPlateOffset.x : -_turnPlateOffset.x, _turnPlateOffset.y);
        }

        private void Update()
        {
            if (_turnScore.Tick(Time.deltaTime, _turn.rectTransform))
                NumberText.Set(_turn, _turnPrefix, _turnScore.Shown, _turnSuffix);
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
            // Подложка видна только с текстом: подсказка читается на любом фоне стола.
            if (_hintPlate != null)
                _hintPlate.SetActive(!string.IsNullOrEmpty(text));
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
            // Меняется на каждый выбор кости: число без сборки строки, текст — только если очки изменились.
            _bankText.Set(_bank.Label, T("match.bankScore"), bankScore);
        }

        public override void OnBackPressed()
        {
            PauseRequested?.Invoke();
        }

        public void HideButtons()
        {
            _roll.SetVisible(false);
            _bank.SetVisible(false);
        }

        /// <summary>Крупное событие партии: плашка-взрыв. В старом префабе без неё — обычная надпись.</summary>
        public UniTask BurstAsync(string word, string caption, Color color, bool fire, float hold, CancellationToken ct)
        {
            return _burst != null
                ? _burst.PlayAsync(word, caption, color, fire, hold, ct)
                : ToastAsync(word, color, hold, ct);
        }

        /// <summary>Ход «в огне» после горячих костей: горят табличка очков и кнопка броска. Гаснет с концом хода.</summary>
        public void SetHot(bool hot)
        {
            if (hot == _hot)
                return;

            _hot = hot;
            if (_turnFire != null)
                _turnFire.SetActive(hot);
            if (_rollFire != null)
                _rollFire.SetActive(hot);
            if (_turnPlate != null)
                _turnPlate.color = hot ? _turnPlateHot : _turnPlateNormal;
        }

        public UniTask ToastAsync(string text, Color color, float duration, CancellationToken ct)
        {
            return Toast.ShowAsync(_kit, (RectTransform)transform, text, color, duration, ct);
        }
    }
}
