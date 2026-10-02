using System;
using System.Collections.Generic;
using Base.Core.Localization;
using UnityEngine;
using Zonk.Progress;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Переключатель сохранённых наборов костей: кнопка на каждый набор (копии шаблона). Смена костей сразу
    /// сохраняется в активный набор, поэтому кнопки «Сохранить» нет. После переключения окно перерисовывает кости.
    /// </summary>
    public sealed class DicePresetsView : MonoBehaviour
    {
        [SerializeField] private RectTransform _list;
        [SerializeField] private UiButtonView _template;

        private readonly List<UiButtonView> _buttons = new List<UiButtonView>();
        private ILoadout _loadout;
        private Action _changed;

#if UNITY_EDITOR
        public void EditorSetup(RectTransform list, UiButtonView template)
        {
            _list = list;
            _template = template;
        }
#endif

        public void Setup(ILocalization localization, ILoadout loadout, Action changed)
        {
            _loadout = loadout;
            _changed = changed;
            _template.gameObject.SetActive(false);

            foreach (var button in _buttons)
                Destroy(button.gameObject);
            _buttons.Clear();

            for (var i = 0; i < loadout.PresetCount; i++)
            {
                var index = i;
                var button = _template.Spawn(_list);
                button.SetText(UiFormat.Format(localization, "presets.slot", i + 1));
                button.OnClick(() => Select(index));
                _buttons.Add(button);
            }

            Refresh();
        }

        private void Select(int index)
        {
            _loadout.SelectPreset(index);
            Refresh();
            _changed?.Invoke();
        }

        private void Refresh()
        {
            for (var i = 0; i < _buttons.Count; i++)
                _buttons[i].SetColor(i == _loadout.ActivePreset ? UiColors.ButtonAccent : UiColors.ButtonMuted);
        }
    }
}
