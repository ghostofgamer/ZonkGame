using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Выбор находки после победы в «Бесконечном забеге»: заголовок и кнопка на каждую находку (копии неактивного
    /// шаблона). Возвращает номер выбранной. Закрыть без выбора нельзя: находка — часть забега.
    /// </summary>
    public sealed class PerkChoiceWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _options;
        [SerializeField] private UiButtonView _optionTemplate;

        private readonly Choice<int> _choice = new Choice<int>();
        private readonly List<UiButtonView> _spawned = new List<UiButtonView>();

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform options, UiButtonView optionTemplate)
        {
            _title = title;
            _options = options;
            _optionTemplate = optionTemplate;
        }
#endif

        private void Awake()
        {
            _optionTemplate.gameObject.SetActive(false);
        }

        public void Setup(string title, IReadOnlyList<string> options)
        {
            _title.text = title;
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
                button.SetText(options[i]);
                button.OnClick(() => _choice.Set(index));
                _spawned.Add(button);
            }
        }

        public UniTask<int> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }
    }
}
