using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>Вариант в окне выбора: текст, цвет кнопки (редкость), можно ли нажать.</summary>
    public readonly struct PerkOption
    {
        public PerkOption(string text, Color color, bool interactable = true)
        {
            Text = text;
            Color = color;
            Interactable = interactable;
        }

        public string Text { get; }
        public Color Color { get; }
        public bool Interactable { get; }
    }

    /// <summary>
    /// Выбор в «Бесконечном забеге»: находка после победы, путь на развилке, покупка в лавке. Заголовок, подзаголовок
    /// (жетоны, сердца, заряды), кнопка на каждый вариант (копии неактивного шаблона, цвет — редкость) и дополнительные
    /// кнопки снизу (за рекламу ещё одна, поменять, уйти). Возвращает номер варианта (0…) или дополнительной кнопки
    /// (−1 — первая, −2 — вторая…). Без подзаголовка и ряда кнопок (старый префаб) — дополнительные кнопки в общем ряду.
    /// </summary>
    public sealed class PerkChoiceWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _options;
        [SerializeField] private UiButtonView _optionTemplate;

        [Tooltip("Строка под заголовком (необязательно)")]
        [SerializeField] private TMP_Text _subtitle;

        [Tooltip("Ряд дополнительных кнопок внизу (необязательно)")]
        [SerializeField] private RectTransform _extras;

        private readonly Choice<int> _choice = new Choice<int>();
        private readonly List<UiButtonView> _spawned = new List<UiButtonView>();

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform options, UiButtonView optionTemplate, TMP_Text subtitle = null,
            RectTransform extras = null)
        {
            _title = title;
            _options = options;
            _optionTemplate = optionTemplate;
            _subtitle = subtitle;
            _extras = extras;
        }
#endif

        private void Awake()
        {
            _optionTemplate.gameObject.SetActive(false);
        }

        public void Setup(string title, IReadOnlyList<string> options)
        {
            var list = new List<PerkOption>(options.Count);
            foreach (var option in options)
                list.Add(new PerkOption(option, UiColors.Button));
            Setup(title, null, list, null);
        }

        public void Setup(string title, string subtitle, IReadOnlyList<PerkOption> options, IReadOnlyList<string> extras)
        {
            _title.text = title;
            if (_subtitle != null)
            {
                _subtitle.text = subtitle ?? string.Empty;
                _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }
            else if (!string.IsNullOrEmpty(subtitle))
            {
                _title.text = title + "\n<size=60%>" + subtitle + "</size>";
            }

            foreach (var button in _spawned)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }

            _spawned.Clear();
            for (var i = 0; i < options.Count; i++)
            {
                var index = i;
                var button = _optionTemplate.Spawn(_options);
                button.SetText(options[i].Text);
                button.SetColor(options[i].Color);
                button.Interactable = options[i].Interactable;
                button.OnClick(() => _choice.Set(index));
                _spawned.Add(button);
            }

            if (extras == null)
                return;

            var parent = _extras != null ? _extras : _options;
            for (var i = 0; i < extras.Count; i++)
            {
                var code = -1 - i;
                var button = _optionTemplate.Spawn(parent);
                button.SetText(extras[i]);
                button.SetColor(UiColors.ButtonMuted);
                button.OnClick(() => _choice.Set(code));
                _spawned.Add(button);
            }
        }

        public UniTask<int> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }
    }
}
