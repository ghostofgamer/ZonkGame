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

        public string Language
        {
            get => Data.Language;
            set => Set(() => Data.Language = value);
        }

        private void Set(Action change)
        {
            change();
            _saves.RequestSave();
            Changed?.Invoke();
        }
    }
}
