using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>Что выбрано в окне паузы. Звук и музыка меняются прямо в окне, это не выбор.</summary>
    public enum PauseChoice
    {
        Resume,
        Rules,
        ExitToMenu,
        Surrender,
    }

    /// <summary>
    /// Пауза партии. Партию на это время останавливает MatchRunner (Time.timeScale = 0), окна идут в реальном времени.
    /// Открывается кнопкой в HUD, кнопкой «Назад» или Escape и при потере фокуса.
    /// </summary>
    public sealed class PauseWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private UiButtonView _resume;
        [SerializeField] private UiButtonView _sound;
        [SerializeField] private UiButtonView _music;
        [SerializeField] private UiButtonView _rules;
        [SerializeField] private UiButtonView _exit;
        [SerializeField] private UiButtonView _surrender;

        private readonly Choice<PauseChoice> _choice = new Choice<PauseChoice>();
        private IGameSettings _settings;

        protected override UiButtonView BackButton => _resume;

        [Inject]
        public void Construct(IGameSettings settings)
        {
            _settings = settings;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, UiButtonView resume, UiButtonView sound, UiButtonView music, UiButtonView rules,
            UiButtonView exit, UiButtonView surrender)
        {
            _title = title;
            _resume = resume;
            _sound = sound;
            _music = music;
            _rules = rules;
            _exit = exit;
            _surrender = surrender;
        }
#endif

        private void Awake()
        {
            _resume.OnClick(() => _choice.Set(PauseChoice.Resume));
            _rules.OnClick(() => _choice.Set(PauseChoice.Rules));
            _exit.OnClick(() => _choice.Set(PauseChoice.ExitToMenu));
            _surrender.OnClick(() => _choice.Set(PauseChoice.Surrender));
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
        }

        protected override void OnShowing()
        {
            Refresh();
        }

        public UniTask<PauseChoice> WaitChoiceAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }

        private void Refresh()
        {
            _title.text = T("match.pause");
            _resume.SetText(T("match.resume"));
            _sound.SetText(T(_settings.Sound ? "settings.soundOn" : "settings.soundOff"));
            _music.SetText(T(_settings.Music ? "settings.musicOn" : "settings.musicOff"));
            _rules.SetText(T("menu.rules"));
            _exit.SetText(T("match.exitMenu"));
            _surrender.SetText(T("match.surrender"));
        }
    }
}
