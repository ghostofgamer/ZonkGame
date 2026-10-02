using System;
using System.Collections.Generic;
using Base.Services.Saves;
using Zonk.Configs;

namespace Zonk.Progress
{
    /// <summary>Что дала победа на этаже башни.</summary>
    public sealed class TowerWin
    {
        /// <summary>Номер этажа с 1.</summary>
        public int Floor;
        public bool FirstClear;
        public bool CheckpointReached;
        public bool TowerCompleted;
        public readonly List<Reward> Rewards = new List<Reward>();
    }

    /// <summary>
    /// Прогресс башни без окон. Попытка начинается с рубежа (пройденные навсегда этажи), на попытку — сердца.
    /// Награда этажа — только за первое прохождение, повторное — монеты. Башня пройдена — новые попытки идут
    /// с первого этажа (награды уже получены). Добавили этажи в конфиг — продолжение с рубежа, на котором остановились.
    /// </summary>
    public sealed class TowerProgress
    {
        private readonly ISaveStore _saves;
        private readonly GameConfig _config;

        public TowerProgress(ISaveStore saves, GameConfig config)
        {
            _saves = saves;
            _config = config;
        }

        public TowerConfig Config => _config != null ? _config.Tower : null;
        public int FloorCount => Config != null ? Config.Floors.Count : 0;
        public bool IsConfigured => FloorCount > 0;
        private TowerSave Data => _saves.Get<TowerSave>(SaveKeys.Tower);

        public bool IsActive => Data.Active && Data.Floor < FloorCount;
        public int Checkpoint => Math.Min(Data.Checkpoint, FloorCount);
        public int Best => Math.Min(Data.Best, FloorCount);
        public int Hearts => Data.Hearts;

        /// <summary>Все этажи пройдены (новые этажи из обновления снимают это состояние сами).</summary>
        public bool IsComplete => FloorCount > 0 && Data.Checkpoint >= FloorCount;

        /// <summary>Индекс этажа (с 0), который сейчас играется или с которого начнётся попытка.</summary>
        public int FloorIndex => IsActive ? Data.Floor : IsComplete ? 0 : Checkpoint;

        public TowerFloor CurrentFloor => FloorIndex < FloorCount ? Config.Floors[FloorIndex] : null;

        public bool CanRevive => Config != null && Config.ReviveForAd && IsActive && !Data.ReviveUsed && Data.Hearts <= 0;

        /// <summary>Первое прохождение этажа ещё впереди: за него полная награда.</summary>
        public bool IsFirstClear(int index) => !Data.Cleared.Contains(index);

        /// <summary>Новая попытка с рубежа. extraHearts — сердца сверх обычных (таланты).</summary>
        public void StartAttempt(int extraHearts = 0)
        {
            var data = Data;
            data.Floor = IsComplete ? 0 : Checkpoint;
            data.Active = true;
            data.Hearts = Math.Max(1, Config.HeartsPerAttempt + Math.Max(0, extraHearts));
            data.ReviveUsed = false;
            data.FreeRevivesUsed = 0;
            _saves.RequestSave();
        }

        /// <summary>Бесплатное продолжение (таланты): free — сколько их положено на попытку.</summary>
        public bool CanReviveFree(int free) => IsActive && Data.Hearts <= 0 && Data.FreeRevivesUsed < free;

        public void ReviveFree()
        {
            var data = Data;
            data.FreeRevivesUsed++;
            data.Hearts = 1;
            _saves.RequestSave();
        }

        public TowerWin OnWin()
        {
            var config = Config;
            var data = Data;
            var index = data.Floor;
            var win = new TowerWin { Floor = index + 1, FirstClear = !data.Cleared.Contains(index) };

            var floor = index < config.Floors.Count ? config.Floors[index] : null;
            if (win.FirstClear)
            {
                data.Cleared.Add(index);
                if (floor != null)
                {
                    foreach (var reward in floor.FirstClearRewards)
                    {
                        if (reward != null)
                            win.Rewards.Add(reward);
                    }
                }
            }
            else if (config.RepeatCoins > 0 && _config.Coins != null)
            {
                win.Rewards.Add(new CurrencyReward { Currency = _config.Coins, Amount = config.RepeatCoins });
            }

            data.Best = Math.Max(data.Best, index + 1);
            data.Floor = index + 1;

            // Рубеж: каждые CheckpointEvery этажей и вершина — дальше попытки начинаются отсюда.
            var every = Math.Max(1, config.CheckpointEvery);
            if ((data.Floor % every == 0 || data.Floor >= config.Floors.Count) && data.Floor > data.Checkpoint)
            {
                data.Checkpoint = data.Floor;
                win.CheckpointReached = true;
            }

            if (data.Floor >= config.Floors.Count)
            {
                data.Active = false;
                win.TowerCompleted = true;
            }

            _saves.RequestSave();
            return win;
        }

        /// <summary>Этаж проигран: минус сердце. Возвращает, сколько осталось (0 — попытка кончается или продолжение за рекламу).</summary>
        public int OnLoss()
        {
            var data = Data;
            data.Hearts = Math.Max(0, data.Hearts - 1);
            _saves.RequestSave();
            return data.Hearts;
        }

        public void Revive()
        {
            if (!CanRevive)
                return;

            var data = Data;
            data.Hearts = 1;
            data.ReviveUsed = true;
            _saves.RequestSave();
        }

        public void EndAttempt()
        {
            Data.Active = false;
            _saves.RequestSave();
        }
    }
}
