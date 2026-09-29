using System;
using System.Collections.Generic;
using Zonk.Core.Dice;
using Zonk.Core.Match;

namespace Zonk.Core.Ai
{
    /// <summary>Характер соперника: тактика выбора костей, риск и доля ошибок.</summary>
    public sealed class AiProfile
    {
        public AiSelectionPolicy Selection = new GreedySelection();
        public AiRiskPolicy Risk = new ThresholdRisk();

        /// <summary>Вероятность отложить случайный допустимый вариант вместо лучшего. Слабые соперники ошибаются чаще.</summary>
        public double MistakeChance;

        public static AiProfile CreateDefault()
        {
            return new AiProfile();
        }
    }

    /// <summary>Решение на один бросок: что отложить и забрать ли после этого очки.</summary>
    public readonly struct TurnDecision
    {
        public TurnDecision(IReadOnlyList<int> keep, bool bank)
        {
            Keep = keep;
            Bank = bank;
        }

        public IReadOnlyList<int> Keep { get; }
        public bool Bank { get; }
    }

    /// <summary>
    /// Решения ИИ поверх профиля. Работает только с открытым состоянием партии и своим ГСЧ,
    /// отдельным от ГСЧ костей: ИИ не может подсмотреть или изменить броски.
    /// </summary>
    public static class AiBrain
    {
        public static TurnDecision Decide(ZonkMatch match, AiProfile profile, IRandom random)
        {
            if (match.Phase != MatchPhase.AwaitingKeep)
                throw new InvalidOperationException($"AI decides only after a roll, phase is {match.Phase}");

            var options = match.GetKeepOptions();
            var context = new AiContext(match);

            var option = profile.MistakeChance > 0 && random.NextDouble() < profile.MistakeChance
                ? options[random.Next(options.Count)]
                : profile.Selection.Choose(context, options, random) ?? options[0];

            var turnScore = match.TurnScore + option.Score;
            var bank = ShouldBank(match, profile, context, turnScore, option.DiceLeft, random);
            return new TurnDecision(option.Dice, bank);
        }

        private static bool ShouldBank(ZonkMatch match, AiProfile profile, AiContext context, int turnScore, int diceLeft,
            IRandom random)
        {
            if (!match.IsBankAllowed(turnScore))
                return false;

            // Бросать нечем (горячие кости выключены): остаётся только забрать.
            if (diceLeft == 0)
                return true;

            var total = context.MyScore + turnScore;

            if (match.IsFinalRound)
            {
                // Последний шанс: забирать есть смысл только обогнав лидера, иначе терять нечего.
                return total > context.BestOpponentScore;
            }

            if (total >= context.TargetScore)
                return true;

            return profile.Risk.ShouldBank(context, turnScore, diceLeft, random);
        }
    }
}
