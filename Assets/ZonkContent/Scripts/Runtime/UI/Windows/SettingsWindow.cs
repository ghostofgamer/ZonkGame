using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Base.Services.Haptics;
using Zenject;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Настройки: звук, музыка, скорость анимаций и язык. Кнопка языка — флаг и название текущего языка
    /// (деталь Parts/LanguageButton), открывает окно выбора языка. Закрытие — WaitCloseRequestAsync.
    /// </summary>
    public sealed class SettingsWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private UiButtonView _sound;
        [SerializeField] private UiButtonView _music;
        [SerializeField] private UiButtonView _speed;

        [Tooltip("Вибрация (показывается только там, где она есть). Пусто — кнопки нет")]
        [SerializeField] private UiButtonView _vibration;
        [SerializeField] private UiButtonView _back;

        [Tooltip("Кнопка текущего языка (копия Parts/LanguageButton). Пусто — выбора языка в окне нет")]
        [SerializeField] private LanguageButtonView _language;

        private IGameSettings _settings;
        private IHapticsService _haptics;
        private IUiService _ui;
        private LanguagePreference _languages;
        private UiConfig _uiConfig;
        private bool _choosing;

        [Inject]
        public void Construct(IGameSettings settings, IUiService ui, LanguagePreference languages, GameConfig config,
            IHapticsService haptics)
        {
            _settings = settings;
            _haptics = haptics;
            _ui = ui;
            _languages = languages;
            _uiConfig = config != null ? config.Ui : null;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, UiButtonView sound, UiButtonView music, UiButtonView speed, UiButtonView back,
            LanguageButtonView language, UiButtonView vibration = null)
        {
            _title = title;
            _sound = sound;
            _music = music;
            _speed = speed;
            _back = back;
            _language = language;
            _vibration = vibration;
        }
#endif

        private void Awake()
        {
            _sound.OnClick(() =>
            {
                _settings.Sound = !_settings.Sound;
                Refresh();
            });
            _music.OnClick(() =>
            {
                _settings.Music = !_settings.Music;
                Refresh();
            });
            _speed.OnClick(() =>
            {
                _settings.Speed = _settings.Speed >= 2 ? 1 : 2;
                Refresh();
            });
            if (_vibration != null)
            {
                _vibration.OnClick(() =>
                {
                    _settings.Vibration = !_settings.Vibration;
                    if (_settings.Vibration)
                        _haptics?.Pulse(40);
                    Refresh();
                });
            }

            _back.OnClick(RequestClose);
        }

        protected override void OnShowing()
        {
            Refresh();
        }

        private void Refresh()
        {
            _title.text = T("menu.settings");
            _back.SetText(T("ui.back"));
            _sound.SetText(T(_settings.Sound ? "settings.soundOn" : "settings.soundOff"));
            _music.SetText(T(_settings.Music ? "settings.musicOn" : "settings.musicOff"));
            _speed.SetText(T("settings.speed", _settings.Speed));
            if (_vibration != null)
            {
                var supported = _haptics != null && _haptics.IsSupported;
                _vibration.gameObject.SetActive(supported);
                if (supported)
                    _vibration.SetText(T(_settings.Vibration ? "settings.vibrationOn" : "settings.vibrationOff"));
            }

            if (_language != null)
            {
                var code = Localization.Language;
                _language.Setup(LanguageWindow.NativeName(code, Localization), _uiConfig != null ? _uiConfig.FlagOf(code) : null, false,
                    () => ChooseLanguageAsync().Forget());
            }
        }

        private async UniTaskVoid ChooseLanguageAsync()
        {
            if (_choosing || _ui == null)
                return;

            _choosing = true;
            try
            {
                var lifetime = this.GetCancellationTokenOnDestroy();
                var window = await _ui.OpenAsync<LanguageWindow>(lifetime);
                string code;
                try
                {
                    code = await window.WaitChoiceAsync(lifetime);
                }
                finally
                {
                    await _ui.CloseAsync(window, CancellationToken.None);
                }

                if (this == null || string.IsNullOrEmpty(code) || code == Localization.Language)
                    return;

                // Язык сразу во всём: окна за настройками откроются заново уже на нём.
                _languages.Choose(code);
                Refresh();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _choosing = false;
            }
        }
    }
}
