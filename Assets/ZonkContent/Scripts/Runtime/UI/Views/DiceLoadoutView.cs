using System;
using System.Collections.Generic;
using Base.Core.Localization;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Шесть кнопок-костей в префабе: нажатие перебирает открытые кости. Особых не больше лимита,
    /// каждая особая кость — не больше чем в одном слоте.
    /// Используется в игре вдвоём (у каждого свой ряд) и в кампании.
    /// </summary>
    public sealed class DiceLoadoutView : MonoBehaviour
    {
        [SerializeField] private UiButtonView[] _slots = new UiButtonView[6];

        private readonly List<DieConfig> _dice = new List<DieConfig>();
        private List<DieConfig> _available;
        private int _maxSpecial;
        private Action<int, DieConfig> _changed;
        private ILocalization _localization;
        private Func<DieConfig, int> _mastery;
        private IReadOnlyList<MasteryLevel> _levels;

        public IReadOnlyList<DieConfig> Dice => _dice;

#if UNITY_EDITOR
        public void EditorSetup(UiButtonView[] slots)
        {
            _slots = slots;
        }
#endif

        /// <param name="mastery">Уровень мастерства кости (необязательно): рядом с названием римская цифра цветом уровня.</param>
        public void Setup(ILocalization localization, List<DieConfig> available, IReadOnlyList<DieConfig> current, int maxSpecial,
            Action<int, DieConfig> changed, Func<DieConfig, int> mastery = null, IReadOnlyList<MasteryLevel> levels = null)
        {
            _mastery = mastery;
            _levels = levels;
            _localization = localization;
            _available = available;
            _maxSpecial = maxSpecial;
            _changed = changed;
            _dice.Clear();
            _dice.AddRange(current);

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = i;
                _slots[i].OnClick(() => Cycle(slot));
            }

            Refresh();
        }

        private void Cycle(int slot)
        {
            if (_available == null || _available.Count == 0 || slot >= _dice.Count)
                return;

            var specialsElsewhere = 0;
            for (var i = 0; i < _dice.Count; i++)
            {
                if (i != slot && _dice[i] != null && _dice[i].IsSpecial)
                    specialsElsewhere++;
            }

            var index = _available.IndexOf(_dice[slot]);
            for (var step = 1; step <= _available.Count; step++)
            {
                var candidate = _available[(index + step + _available.Count) % _available.Count];
                // Особая кость — только в одном слоте; лимит особых — из GameConfig.
                if (candidate.IsSpecial && (specialsElsewhere >= _maxSpecial || UsedElsewhere(candidate, slot)))
                    continue;

                _dice[slot] = candidate;
                _changed?.Invoke(slot, candidate);
                break;
            }

            Refresh();
        }

        private bool UsedElsewhere(DieConfig die, int slot)
        {
            for (var i = 0; i < _dice.Count; i++)
            {
                if (i != slot && _dice[i] == die)
                    return true;
            }

            return false;
        }

        private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        private string MasteryMark(DieConfig die)
        {
            var level = _mastery != null ? _mastery(die) : 0;
            if (level <= 0)
                return string.Empty;

            var color = _levels != null && level <= _levels.Count && _levels[level - 1] != null ? _levels[level - 1].Color : UiColors.Gold;
            var mark = level <= Roman.Length ? Roman[level - 1] : level.ToString();
            return " <color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + mark + "</color>";
        }

        private void Refresh()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var die = i < _dice.Count ? _dice[i] : null;
                _slots[i].SetText(die != null && _localization != null ? _localization.Get(die.NameKey) + MasteryMark(die) : "?");
                _slots[i].SetColor(die != null && die.IsSpecial
                    ? Color.Lerp(UiColors.ButtonMuted, die.MarkerColor, 0.6f)
                    : UiColors.ButtonMuted);
            }
        }
    }
}
