using System;
using System.Collections.Generic;
using Base.Core.Localization;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Шесть кнопок-костей в префабе: нажатие перебирает открытые кости. Особых не больше лимита.
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

        public IReadOnlyList<DieConfig> Dice => _dice;

#if UNITY_EDITOR
        public void EditorSetup(UiButtonView[] slots)
        {
            _slots = slots;
        }
#endif

        public void Setup(ILocalization localization, List<DieConfig> available, IReadOnlyList<DieConfig> current, int maxSpecial,
            Action<int, DieConfig> changed)
        {
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
                if (candidate.IsSpecial && specialsElsewhere >= _maxSpecial)
                    continue;

                _dice[slot] = candidate;
                _changed?.Invoke(slot, candidate);
                break;
            }

            Refresh();
        }

        private void Refresh()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var die = i < _dice.Count ? _dice[i] : null;
                _slots[i].SetText(die != null && _localization != null ? _localization.Get(die.NameKey) : "?");
                _slots[i].SetColor(die != null && die.IsSpecial
                    ? Color.Lerp(UiColors.ButtonMuted, die.MarkerColor, 0.6f)
                    : UiColors.ButtonMuted);
            }
        }
    }
}
