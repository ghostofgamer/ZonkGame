using System;
using System.Threading;
using Base.Core.Localization;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Progress;
using Zonk.Utils;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Уровень игрока (деталь Parts/PlayerLevel): номер уровня, полоса опыта, «опыт / нужно».
    /// В меню — текущее состояние, в итогах партии — полоса заполняется набранным опытом (через новые уровни тоже).
    /// Полоса — RectTransform, растянутый от левого края: ширина задаётся якорем anchorMax.x.
    /// </summary>
    public sealed class PlayerLevelView : MonoBehaviour
    {
        private const float FillDuration = 0.9f;

        [SerializeField] private TMP_Text _level;
        [SerializeField] private RectTransform _fill;

        [Tooltip("«120 / 380» или «+60 опыта» после партии (необязательно)")]
        [SerializeField] private TMP_Text _caption;

        [Tooltip("Значок «есть свободные очки талантов» (необязательно)")]
        [SerializeField] private GameObject _talentBadge;

        private ILocalization _localization;
        private ITalents _talents;

        /// <summary>Нажали на уровень (в меню открывает профиль). Кнопка — компонент Button на детали, если есть.</summary>
        public event Action Clicked;

        [Inject]
        public void Construct([InjectOptional] ILocalization localization, [InjectOptional] ITalents talents)
        {
            _talents = talents;
            _localization = localization;
        }

#if UNITY_EDITOR
        public void EditorSetupBadge(GameObject talentBadge)
        {
            _talentBadge = talentBadge;
        }

        public void EditorSetup(TMP_Text level, RectTransform fill, TMP_Text caption)
        {
            _level = level;
            _fill = fill;
            _caption = caption;
        }
#endif

        private void Awake()
        {
            var button = GetComponent<UnityEngine.UI.Button>();
            if (button != null)
                button.onClick.AddListener(() => Clicked?.Invoke());
        }

        /// <summary>Текущий уровень и опыт.</summary>
        public void Show(IPlayerLevel level)
        {
            if (level == null)
                return;

            level.GetProgress(level.Xp, out var number, out var into, out var toNext);
            SetLevel(number);
            SetFill(Fraction(into, toNext));
            if (_caption != null)
                _caption.text = into + " / " + toNext;
            if (_talentBadge != null)
                _talentBadge.SetActive(_talents != null && _talents.FreePoints > 0);
        }

        /// <summary>Опыт за партию: подпись «+N опыта» и заполнение полосы от «было» до «стало».</summary>
        public void PlayGain(XpGain gain, IPlayerLevel level)
        {
            if (level == null)
                return;

            if (_caption != null)
            {
                var text = Localize("level.xpGained", gain.MatchXp);
                if (gain.FirstWinXp > 0)
                    text += "  " + Localize("level.firstWin", gain.FirstWinXp);
                _caption.text = text;
            }

            PlayFillAsync(gain, level, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid PlayFillAsync(XpGain gain, IPlayerLevel level, CancellationToken ct)
        {
            level.GetProgress(gain.XpBefore, out var from, out var fromInto, out var fromNeed);
            level.GetProgress(gain.XpAfter, out var to, out var toInto, out var toNeed);
            SetLevel(from);
            SetFill(Fraction(fromInto, fromNeed));

            // Каждый пройденный уровень — полоса до конца и заново; последний — до набранного.
            var current = from;
            var start = Fraction(fromInto, fromNeed);
            while (!ct.IsCancellationRequested)
            {
                var end = current < to ? 1f : Fraction(toInto, toNeed);
                for (var time = 0f; time < FillDuration; time += Time.unscaledDeltaTime)
                {
                    SetFill(Mathf.Lerp(start, end, AnimateEase.OutCubic(time / FillDuration)));
                    await UniTask.Yield();
                    if (ct.IsCancellationRequested)
                        return;
                }

                SetFill(end);
                if (current >= to)
                    return;

                current++;
                SetLevel(current);
                start = 0f;
            }
        }

        private void SetLevel(int number)
        {
            if (_level != null)
                _level.text = number.ToString();
        }

        private void SetFill(float fraction)
        {
            if (_fill == null)
                return;

            var max = _fill.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            _fill.anchorMax = max;
        }

        private static float Fraction(long into, long toNext)
        {
            return toNext > 0 ? Mathf.Clamp01((float)into / toNext) : 1f;
        }

        private string Localize(string key, int value)
        {
            return UiFormat.Format(_localization, key, value);
        }
    }
}
