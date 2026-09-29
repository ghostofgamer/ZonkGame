using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Match;

namespace Zonk.UI
{
    /// <summary>
    /// Шесть кнопок-костей: нажатие перебирает открытые кости. Особых не больше лимита.
    /// Используется в игре вдвоём (у каждого свой ряд) и в кампании.
    /// </summary>
    public sealed class DiceLoadoutRow
    {
        private readonly UiKit _kit;
        private readonly List<DieConfig> _available;
        private readonly List<DieConfig> _dice;
        private readonly int _maxSpecial;
        private readonly Action<int, DieConfig> _changed;
        private readonly UiButton[] _buttons = new UiButton[ZonkMatch.DiceCount];

        public DiceLoadoutRow(UiKit kit, Transform parent, List<DieConfig> available, IReadOnlyList<DieConfig> current,
            int maxSpecial, Action<int, DieConfig> changed)
        {
            _kit = kit;
            _available = available;
            _dice = new List<DieConfig>(current);
            _maxSpecial = maxSpecial;
            _changed = changed;

            var row = kit.Row(parent, 8);
            UiKit.Size(row, -1, 84);
            for (var i = 0; i < _buttons.Length; i++)
            {
                var slot = i;
                _buttons[i] = kit.Button(row.transform, string.Empty, () => Cycle(slot), null, 18);
            }

            Refresh();
        }

        public IReadOnlyList<DieConfig> Dice => _dice;

        private void Cycle(int slot)
        {
            if (_available.Count == 0)
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
            for (var i = 0; i < _buttons.Length; i++)
            {
                var die = _dice[i];
                _buttons[i].SetText(die != null ? _kit.T(die.NameKey) : "?");
                _buttons[i].SetColor(die != null && die.IsSpecial
                    ? Color.Lerp(UiColors.ButtonMuted, die.MarkerColor, 0.6f)
                    : UiColors.ButtonMuted);
            }
        }
    }
}
