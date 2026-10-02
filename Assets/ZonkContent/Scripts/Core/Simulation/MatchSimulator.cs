using System;
using System.Collections.Generic;
using System.Text;
using Zonk.Core.Ai;
using Zonk.Core.Dice;
using Zonk.Core.Match;

namespace Zonk.Core.Simulation
{
    /// <summary>Итог серии партий ИИ против ИИ.</summary>
    public sealed class SimulationResult
    {
        public int Matches;
        public int[] Wins;
        public long[] Turns;
        public long[] Banked;
        public long[] Zonks;
        public long TotalTurns;

        /// <summary>Доля побед игрока.</summary>
        public double WinRate(int player) => Matches > 0 ? (double)Wins[player] / Matches : 0;

        /// <summary>Средние очки за ход с учётом Зонков (они дают 0).</summary>
        public double AverageTurnScore(int player) => Turns[player] > 0 ? (double)Banked[player] / Turns[player] : 0;

        /// <summary>Доля ходов, закончившихся Зонком.</summary>
        public double ZonkRate(int player) => Turns[player] > 0 ? (double)Zonks[player] / Turns[player] : 0;

        /// <summary>Средняя длина партии в ходах обоих игроков.</summary>
        public double AverageMatchTurns => Matches > 0 ? (double)TotalTurns / Matches : 0;

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append($"matches={Matches} avgTurns={AverageMatchTurns:F1}");
            for (var i = 0; i < Wins.Length; i++)
                text.Append($" | p{i}: win={WinRate(i):P1} avgTurn={AverageTurnScore(i):F0} zonk={ZonkRate(i):P1}");
            return text.ToString();
        }
    }

    /// <summary>
    /// Симулятор баланса: гоняет партии ИИ против ИИ на ядре правил. Первый ход чередуется,
    /// чтобы преимущество первого хода не искажало итог. Используется тестами баланса и окном в редакторе.
    /// </summary>
    public static class MatchSimulator
    {
        private const int MaxTurnsPerMatch = 2000;

        public static SimulationResult Run(MatchSettings template, IReadOnlyList<AiProfile> profiles, int matches, ulong seed)
        {
            var players = template.Players.Count;
            if (profiles.Count != players)
                throw new ArgumentException("One AI profile per player is required");

            var result = new SimulationResult
            {
                Matches = matches,
                Wins = new int[players],
                Turns = new long[players],
                Banked = new long[players],
                Zonks = new long[players],
            };

            var seeds = new SplitMixRandom(seed);
            for (var m = 0; m < matches; m++)
            {
                var settings = new MatchSettings
                {
                    Rules = template.Rules,
                    Players = template.Players,
                    Modifiers = template.Modifiers,
                    Seed = (ulong)(seeds.NextDouble() * ulong.MaxValue),
                    FirstPlayer = m % players,
                };

                var aiRandom = new SplitMixRandom(settings.Seed ^ 0x5DEECE66DUL);
                PlayMatch(new ZonkMatch(settings), profiles, aiRandom, result);
            }

            return result;
        }

        /// <summary>Доигрывает партию решениями ИИ и копит статистику.</summary>
        public static void PlayMatch(ZonkMatch match, IReadOnlyList<AiProfile> profiles, IRandom aiRandom,
            SimulationResult stats = null)
        {
            var turns = 0;
            while (match.Phase != MatchPhase.Finished && turns < MaxTurnsPerMatch)
            {
                var player = match.CurrentPlayerIndex;
                var end = PlayTurn(match, profiles[player], aiRandom);
                turns++;

                if (stats == null)
                    continue;

                stats.Turns[player]++;
                stats.TotalTurns++;
                if (end.IsZonk)
                    stats.Zonks[player]++;
                else
                    stats.Banked[player] += end.Banked;
            }

            if (stats != null && match.Winner != null)
                stats.Wins[match.Winner.Index]++;
        }

        private static TurnEnd PlayTurn(ZonkMatch match, AiProfile profile, IRandom aiRandom)
        {
            while (true)
            {
                var roll = match.Roll();
                if (roll.TurnEnd != null)
                    return roll.TurnEnd;

                // Заряд спасения сгорел: те же кости бросаются снова.
                if (roll.ZonkSaved)
                    continue;

                var decision = AiBrain.Decide(match, profile, aiRandom);
                match.Keep(decision.Keep);

                if (decision.Bank || !match.CanRollAgain)
                {
                    if (match.CanBank)
                        return match.Bank();

                    if (!match.CanRollAgain)
                        throw new InvalidOperationException("Turn is stuck: no dice to roll and banking is not allowed");
                }
            }
        }

        /// <summary>Набор из шести одинаковых костей.</summary>
        public static IReadOnlyList<DieSpec> Uniform(DieSpec die)
        {
            var dice = new DieSpec[ZonkMatch.DiceCount];
            for (var i = 0; i < dice.Length; i++)
                dice[i] = die;
            return dice;
        }
    }
}
