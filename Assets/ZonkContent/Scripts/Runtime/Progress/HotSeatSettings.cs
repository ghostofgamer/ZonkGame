using System.Collections.Generic;
using Base.Services.Saves;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Match;

namespace Zonk.Progress
{
    /// <summary>
    /// Настройки игры вдвоём: цель, кто первый, особые кости, у каждого игрока имя, кости и скин.
    /// Запоминаются между партиями.
    /// </summary>
    public sealed class HotSeatSettings
    {
        public const int PlayerCount = 2;

        private readonly ISaveStore _saves;
        private readonly GameConfig _config;

        public HotSeatSettings(ISaveStore saves, GameConfig config)
        {
            _saves = saves;
            _config = config;
        }

        public HotSeatSave Data
        {
            get
            {
                var data = _saves.Get<HotSeatSave>(SaveKeys.HotSeat);
                Normalize(data);
                return data;
            }
        }

        public void Save()
        {
            _saves.RequestSave();
        }

        public int ClampTarget(int target)
        {
            var step = Mathf.Max(1, _config.HotSeatTargetStep);
            target = Mathf.Clamp(target, _config.HotSeatMinTarget, _config.HotSeatMaxTarget);
            return Mathf.RoundToInt((float)target / step) * step;
        }

        /// <summary>Кто ходит первым в новой партии по выбранному режиму.</summary>
        public int PickFirstPlayer()
        {
            var data = Data;
            int first;
            switch (data.FirstPlayer)
            {
                case FirstPlayerMode.PlayerOne:
                    first = 0;
                    break;
                case FirstPlayerMode.Alternate:
                    first = 1 - Mathf.Clamp(data.LastFirstPlayer, 0, 1);
                    break;
                default:
                    first = Random.Range(0, PlayerCount);
                    break;
            }

            data.LastFirstPlayer = first;
            Save();
            return first;
        }

        private void Normalize(HotSeatSave data)
        {
            if (data.Target <= 0)
                data.Target = _config.HotSeatDefaultTarget;
            data.Target = ClampTarget(data.Target);

            while (data.Players.Count < PlayerCount)
                data.Players.Add(new HotSeatPlayerSave());

            foreach (var player in data.Players)
            {
                if (player.Dice == null)
                    player.Dice = new List<string>();
                while (player.Dice.Count < ZonkMatch.DiceCount)
                    player.Dice.Add(_config.StandardDie != null ? _config.StandardDie.Id : string.Empty);
            }
        }
    }
}
