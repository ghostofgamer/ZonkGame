using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>Настройки: звук, музыка, скорость анимаций. Закрытие — WaitCloseRequestAsync.</summary>
    public sealed class SettingsWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private UiButtonView _sound;
        [SerializeField] private UiButtonView _music;
        [SerializeField] private UiButtonView _speed;
        [SerializeField] private UiButtonView _back;

        private IGameSettings _settings;

        [Inject]
        public void Construct(IGameSettings settings)
        {
            _settings = settings;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, UiButtonView sound, UiButtonView music, UiButtonView speed, UiButtonView back)
        {
            _title = title;
            _sound = sound;
            _music = music;
            _speed = speed;
            _back = back;
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
            _back.OnClick(RequestClose);
        }

        protected override void OnShowing()
        {
            _title.text = T("menu.settings");
            _back.SetText(T("ui.back"));
            Refresh();
        }

        private void Refresh()
        {
            _sound.SetText(T(_settings.Sound ? "settings.soundOn" : "settings.soundOff"));
            _music.SetText(T(_settings.Music ? "settings.musicOn" : "settings.musicOff"));
            _speed.SetText(T("settings.speed", _settings.Speed));
        }
    }
}
