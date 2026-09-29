using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Выбор языка: кнопка на каждый язык игры (деталь Parts/LanguageButton: флаг и название на самом языке),
    /// текущий отмечен. Возвращает код выбранного языка или null (закрыли).
    /// </summary>
    public sealed class LanguageWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _list;
        [SerializeField] private LanguageButtonView _buttonPrefab;
        [SerializeField] private UiButtonView _close;

        private readonly Choice<string> _choice = new Choice<string>();
        private UiConfig _ui;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform list, LanguageButtonView buttonPrefab, UiButtonView close)
        {
            _title = title;
            _list = list;
            _buttonPrefab = buttonPrefab;
            _close = close;
        }
#endif

        [Inject]
        public void Construct(GameConfig config)
        {
            _ui = config != null ? config.Ui : null;
        }

        private void Awake()
        {
            _close.OnClick(() => _choice.Set(null));
        }

        protected override void OnShowing()
        {
            _title.text = T("language.title");
            _close.SetText(T("ui.back"));

            foreach (Transform child in _list)
                Destroy(child.gameObject);

            foreach (var code in Localization.Languages)
            {
                var captured = code;
                var button = Instantiate(_buttonPrefab, _list);
                button.gameObject.SetActive(true);
                button.Setup(NativeName(code), _ui != null ? _ui.FlagOf(code) : null, code == Localization.Language,
                    () => _choice.Set(captured));
            }
        }

        public UniTask<string> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }

        /// <summary>Название языка на нём самом («Deutsch», «Français»): ключ language.&lt;код&gt;, иначе код.</summary>
        public static string NativeName(string code, Base.Core.Localization.ILocalization localization)
        {
            var key = "language." + code;
            var name = localization.Get(key);
            return name == key ? code.ToUpperInvariant() : name;
        }

        private string NativeName(string code) => NativeName(code, Localization);
    }
}
