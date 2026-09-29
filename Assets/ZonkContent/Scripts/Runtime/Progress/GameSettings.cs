using System;
using Base.Services.Saves;

namespace Zonk.Progress
{
    /// <summary>Настройки игрока: звук, музыка, скорость анимаций.</summary>
    public interface IGameSettings
    {
        bool Sound { get; set; }
        bool Music { get; set; }

        /// <summary>Множитель скорости анимаций: 1 или 2.</summary>
        int Speed { get; set; }

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
            set => Set(() => Data.Sound = value);
        }

        public bool Music
        {
            get => Data.Music;
            set => Set(() => Data.Music = value);
        }

        public int Speed
        {
            get => Data.Speed < 1 ? 1 : Data.Speed;
            set => Set(() => Data.Speed = value < 1 ? 1 : value > 3 ? 3 : value);
        }

        private void Set(Action change)
        {
            change();
            _saves.RequestSave();
            Changed?.Invoke();
        }
    }
}
