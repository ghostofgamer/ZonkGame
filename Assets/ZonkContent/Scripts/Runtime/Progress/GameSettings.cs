using System;
using Base.Services.Saves;

namespace Zonk.Progress
{
    /// <summary>Настройки игрока: звук, музыка, скорость анимаций, выбранный язык.</summary>
    public interface IGameSettings
    {
        bool Sound { get; set; }
        bool Music { get; set; }

        /// <summary>Множитель скорости анимаций: 1 или 2.</summary>
        int Speed { get; set; }

        /// <summary>Вибрация телефона. Где её нет (браузер), настройка не показывается.</summary>
        bool Vibration { get; set; }

        /// <summary>Подсказка лучшего хода (если взят талант BestMoveHint).</summary>
        bool BestMoveHint { get; set; }

        /// <summary>Язык, выбранный игроком. Пусто — язык площадки (определяется при запуске).</summary>
        string Language { get; set; }

        event Action Changed;
    }

    public sealed class GameSettings : IGameSettings
    {
        private readonly ISaveStore _saves;

        public GameSettings(ISaveStore saves)
        {
            _saves = saves;
        }

        public event Action Changed;

        private SettingsSave Data => _saves.Get<SettingsSave>(SaveKeys.Settings);

        public bool Sound
        {
            get => Data.Sound;
            set
            {
                Data.Sound = value;
                Saved();
            }
        }

        public bool Music
        {
            get => Data.Music;
            set
            {
                Data.Music = value;
                Saved();
            }
        }

        public int Speed
        {
            get => Data.Speed < 1 ? 1 : Data.Speed;
            set
            {
                Data.Speed = value < 1 ? 1 : value > 3 ? 3 : value;
                Saved();
            }
        }

        public bool Vibration
        {
            get => Data.Vibration;
            set
            {
                Data.Vibration = value;
                Saved();
            }
        }

        public bool BestMoveHint
        {
            get => Data.BestMoveHint;
            set
            {
                Data.BestMoveHint = value;
                Saved();
            }
        }

        public string Language
        {
            get => Data.Language;
            set
            {
                Data.Language = value;
                Saved();
            }
        }

        private void Saved()
        {
            _saves.RequestSave();
            Changed?.Invoke();
        }
    }
}
