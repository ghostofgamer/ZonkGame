using System;
using System.Collections.Generic;
using Zonk.Core.Dice;

namespace Zonk.Core.Modes
{
    /// <summary>
    /// Как устроен этаж «Бесконечного забега»: цель, сила врага, его особые кости и ИИ, стражи, общие правила.
    /// Только числа и расчёт, без Unity: пригодно для сервера и тестов. Всё, что случайно (какие правила выпали),
    /// выводится из сида забега и номера этажа — после перезапуска игры забег тот же.
    ///
    /// Значения по умолчанию подобраны моделью 3000 забегов с находками 2.0 (README, журнал 2026-10-02): игрок —
    /// ИИ «сбалансированный», разумный выбор находок, без рекламы — медиана 14 этажей, 75% — до 26, 90% — до 46, 99% — до 88.
    /// Мягкий старт (враг слабее, новичок до 8-го), дальше сила врага растёт с ускорением: любая сборка со временем отстаёт.
    /// </summary>
    [Serializable]
    public sealed class EndlessRunPlan
    {
        /// <summary>Цель этажа: TargetBase + TargetPerFloor × этаж, не больше TargetMax.</summary>
        public int TargetBase = 1500;
        public int TargetPerFloor = 50;
        public int TargetMax = 3500;

        /// <summary>Сила очков врага (множитель всех его комбинаций): на первом этаже и прирост за этаж.</summary>
        public float EnemyStartPower = 0.7f;
        public float EnemyPowerPerFloor = 0.04f;

        /// <summary>Ускорение роста силы врага: + EnemyPowerAccel × (этаж − 1)². Любая сборка игрока со временем отстаёт.</summary>
        public float EnemyPowerAccel = 0.0008f;

        /// <summary>Враг получает особую кость каждые EnemyDiceEvery этажей, всего не больше 6.</summary>
        public int EnemyDiceEvery = 8;

        /// <summary>Новое общее правило каждые RulesEvery этажей; действуют последние MaxRules, старые уходят.</summary>
        public int RulesEvery = 5;
        public int MaxRules = 3;

        /// <summary>Каждый GuardianEvery-й этаж — страж (босс).</summary>
        public int GuardianEvery = 10;

        /// <summary>ИИ врага: «новичок» до EarlyUntil-го этажа, «средний» до MidUntil-го, дальше «эксперт».</summary>
        public int EarlyUntil = 8;
        public int MidUntil = 25;

        public int Target(int floor)
        {
            return Math.Max(1, Math.Min(TargetMax, TargetBase + TargetPerFloor * Math.Max(1, floor)));
        }

        public float EnemyPower(int floor)
        {
            var step = Math.Max(1, floor) - 1;
            return Math.Max(0.1f, EnemyStartPower + EnemyPowerPerFloor * step + EnemyPowerAccel * step * step);
        }

        public int EnemySpecialDice(int floor)
        {
            return EnemyDiceEvery <= 0 ? 0 : Math.Min(6, Math.Max(1, floor) / EnemyDiceEvery);
        }

        /// <summary>0 — «новичок», 1 — «средний», 2 — «эксперт».</summary>
        public int AiTier(int floor)
        {
            return floor < EarlyUntil ? 0 : floor < MidUntil ? 1 : 2;
        }

        public bool IsGuardian(int floor)
        {
            return GuardianEvery > 0 && floor > 0 && floor % GuardianEvery == 0;
        }

        /// <summary>На этом этаже появляется новое общее правило.</summary>
        public bool AddsRule(int floor)
        {
            return RulesEvery > 0 && MaxRules > 0 && floor > 0 && floor % RulesEvery == 0;
        }

        /// <summary>
        /// Индексы общих правил из пула размера poolSize, действующих на этаже floor, от старого к новому.
        /// Новое правило не повторяет уже действующие (если пул позволяет). result очищается.
        /// </summary>
        public void ActiveRules(ulong seed, int floor, int poolSize, List<int> result)
        {
            result.Clear();
            if (poolSize <= 0 || RulesEvery <= 0 || MaxRules <= 0)
                return;

            for (var at = RulesEvery; at <= floor; at += RulesEvery)
            {
                if (result.Count >= MaxRules)
                    result.RemoveAt(0);

                var random = FloorRandom(seed, at, 17);
                var free = poolSize - result.Count;
                if (free <= 0)
                {
                    result.Add((int)(random.NextDouble() * poolSize));
                    continue;
                }

                // Выбор среди ещё не действующих: n-й свободный индекс.
                var pick = (int)(random.NextDouble() * free);
                for (var index = 0; index < poolSize; index++)
                {
                    if (result.Contains(index))
                        continue;
                    if (pick-- == 0)
                    {
                        result.Add(index);
                        break;
                    }
                }
            }
        }

        /// <summary>Случайность этажа: одна и та же для сида забега, этажа и назначения (salt).</summary>
        public static SplitMixRandom FloorRandom(ulong seed, int floor, int salt)
        {
            unchecked
            {
                return new SplitMixRandom(seed ^ ((ulong)floor * 0x9E3779B97F4A7C15UL) ^ ((ulong)salt * 0xBF58476D1CE4E5B9UL));
            }
        }
    }
}
