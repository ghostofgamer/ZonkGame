using System;
using System.Collections.Generic;
using Zonk.Core.Dice;
using Zonk.Core.Modifiers;
using Zonk.Core.Rules;

namespace Zonk.Core.Match
{
    /// <summary>
    /// Партия в Зонк как машина состояний без Unity и без асинхронности.
    /// Команды: Roll, Keep, Bank. Каждая возвращает итог, который визуал проигрывает.
    /// Результат броска решает только ГСЧ партии с весами граней костей; физика в игре его лишь показывает.
    /// Этот же класс гоняет симулятор баланса и в будущем будет судьёй на сервере онлайна.
    /// </summary>
    public sealed class ZonkMatch
    {
        public const int DiceCount = 6;

        private readonly IRandom _random;
        private readonly List<MatchPlayer> _players = new List<MatchPlayer>();
        private readonly int[] _faces = new int[DiceCount];
        private readonly bool[] _inHand = new bool[DiceCount];
        private readonly List<MatchModifier> _modifiers;
        private int _finalRoundStarter = -1;

        public ZonkMatch(MatchSettings settings)
        {
            if (settings.Players.Count < 2)
                throw new ArgumentException("A match needs at least two players");

            _modifiers = new List<MatchModifier>(settings.Modifiers);
            Rules = settings.Rules.Clone();
            foreach (var modifier in _modifiers)
                modifier.ModifyRules(Rules);

            Calculator = new ScoreCalculator(Rules.Rules, _modifiers);
            _random = new SplitMixRandom(settings.Seed);

            for (var i = 0; i < settings.Players.Count; i++)
            {
                var setup = settings.Players[i];
                if (setup.Dice == null || setup.Dice.Count != DiceCount)
                    throw new ArgumentException($"Player {i} must have exactly {DiceCount} dice");
                _players.Add(new MatchPlayer(i, setup));
            }

            CurrentPlayerIndex = Math.Max(0, Math.Min(settings.FirstPlayer, _players.Count - 1));
            StartTurn();
        }

        /// <summary>Правила с учётом модификаторов.</summary>
        public RuleSet Rules { get; }

        public ScoreCalculator Calculator { get; }
        public IReadOnlyList<MatchModifier> Modifiers => _modifiers;
        public IReadOnlyList<MatchPlayer> Players => _players;
        public MatchPhase Phase { get; private set; }
        public int CurrentPlayerIndex { get; private set; }
        public MatchPlayer CurrentPlayer => _players[CurrentPlayerIndex];

        /// <summary>Очки текущего хода, ещё не записанные.</summary>
        public int TurnScore { get; private set; }

        /// <summary>Сквозной номер хода с нуля.</summary>
        public int TurnNumber { get; private set; }

        /// <summary>Кто-то дошёл до цели, идут последние ходы остальных.</summary>
        public bool IsFinalRound => _finalRoundStarter >= 0;

        public MatchPlayer Winner { get; private set; }

        /// <summary>Грани шести слотов. 0: кость ещё не брошена в этом ходу.</summary>
        public IReadOnlyList<int> Faces => _faces;

        public bool IsInHand(int die)
        {
            return _inHand[die];
        }

        public int DiceInHandCount
        {
            get
            {
                var count = 0;
                foreach (var inHand in _inHand)
                {
                    if (inHand)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Можно ли сейчас забрать очки: после отложенных костей и с учётом порогов.</summary>
        public bool CanBank => Phase == MatchPhase.AwaitingDecision && IsBankAllowed(TurnScore);

        /// <summary>Пороги входа и минимума для заданных очков хода текущего игрока.</summary>
        public bool IsBankAllowed(int turnScore)
        {
            if (turnScore <= 0)
                return false;
            if (!CurrentPlayer.HasEntered && turnScore < Rules.EntryScore)
                return false;
            return turnScore >= Rules.MinBankScore;
        }

        public RollOutcome Roll()
        {
            if (Phase != MatchPhase.AwaitingRoll && Phase != MatchPhase.AwaitingDecision)
                throw new InvalidOperationException($"Cannot roll in phase {Phase}");

            var rolled = new List<int>(DiceCount);
            var rolledFaces = new List<int>(DiceCount);
            for (var die = 0; die < DiceCount; die++)
            {
                if (!_inHand[die])
                    continue;

                _faces[die] = CurrentPlayer.Dice[die].Roll(_random);
                rolled.Add(die);
                rolledFaces.Add(_faces[die]);
            }

            var outcome = new RollOutcome
            {
                Player = CurrentPlayerIndex,
                RolledDice = rolled,
                Faces = (int[])_faces.Clone(),
            };

            if (Calculator.HasAnyScore(rolledFaces))
            {
                Phase = MatchPhase.AwaitingKeep;
                return outcome;
            }

            outcome.IsZonk = true;
            outcome.TurnEnd = EndTurnWithZonk();
            return outcome;
        }

        /// <summary>Очки за выбранные кости или Invalid, если выбор недопустим.</summary>
        public ScoreResult EvaluateSelection(IReadOnlyList<int> dice)
        {
            if (Phase != MatchPhase.AwaitingKeep || dice == null || dice.Count == 0)
                return ScoreResult.Invalid;

            var faces = new List<int>(dice.Count);
            var seen = new bool[DiceCount];
            foreach (var die in dice)
            {
                if (die < 0 || die >= DiceCount || seen[die] || !_inHand[die] || _faces[die] == 0)
                    return ScoreResult.Invalid;

                seen[die] = true;
                faces.Add(_faces[die]);
            }

            return Calculator.EvaluateFaces(faces);
        }

        public KeepOutcome Keep(IReadOnlyList<int> dice)
        {
            if (Phase != MatchPhase.AwaitingKeep)
                throw new InvalidOperationException($"Cannot keep in phase {Phase}");

            var score = EvaluateSelection(dice);
            if (!score.IsValid)
                throw new InvalidOperationException("Selected dice do not score");

            foreach (var die in dice)
                _inHand[die] = false;

            TurnScore += score.Score;

            var hotDice = false;
            if (DiceInHandCount == 0 && Rules.HotDice)
            {
                hotDice = true;
                for (var die = 0; die < DiceCount; die++)
                {
                    _inHand[die] = true;
                    _faces[die] = 0;
                }
            }

            Phase = MatchPhase.AwaitingDecision;

            // Без горячих костей и без костей в руке продолжать нечем: ход можно только завершить.
            return new KeepOutcome
            {
                Player = CurrentPlayerIndex,
                KeptDice = new List<int>(dice),
                Score = score,
                TurnScore = TurnScore,
                HotDice = hotDice,
            };
        }

        /// <summary>Можно ли бросить ещё: в руке есть кости.</summary>
        public bool CanRollAgain => Phase == MatchPhase.AwaitingDecision && DiceInHandCount > 0;

        public TurnEnd Bank()
        {
            if (!CanBank)
                throw new InvalidOperationException("Cannot bank now");

            var player = CurrentPlayer;
            var banked = TurnScore;
            player.Score += banked;
            player.HasEntered = true;
            player.ZonkStreak = 0;
            player.BestTurn = Math.Max(player.BestTurn, banked);

            var end = new TurnEnd { Player = CurrentPlayerIndex, Banked = banked };

            if (player.Score >= Rules.TargetScore)
            {
                if (!Rules.FinalRound)
                {
                    Finish();
                    end.MatchFinished = true;
                    end.NextPlayer = CurrentPlayerIndex;
                    return end;
                }

                if (!IsFinalRound)
                {
                    _finalRoundStarter = CurrentPlayerIndex;
                    end.StartedFinalRound = true;
                }
            }

            AdvanceTurn(end);
            return end;
        }

        /// <summary>Все допустимые варианты отложить кости, без повторов по набору граней.</summary>
        public IReadOnlyList<KeepOption> GetKeepOptions()
        {
            var options = new List<KeepOption>();
            if (Phase != MatchPhase.AwaitingKeep)
                return options;

            var hand = new List<int>(DiceCount);
            for (var die = 0; die < DiceCount; die++)
            {
                if (_inHand[die])
                    hand.Add(die);
            }

            var seen = new HashSet<int>();
            var subsetCount = 1 << hand.Count;
            for (var mask = 1; mask < subsetCount; mask++)
            {
                var dice = new List<int>(hand.Count);
                var key = 0;
                for (var bit = 0; bit < hand.Count; bit++)
                {
                    if ((mask & (1 << bit)) == 0)
                        continue;
                    dice.Add(hand[bit]);
                    key += Pow7(_faces[hand[bit]] - 1);
                }

                if (!seen.Add(key))
                    continue;

                var score = EvaluateSelection(dice);
                if (!score.IsValid)
                    continue;

                var left = hand.Count - dice.Count;
                if (left == 0 && Rules.HotDice)
                    left = DiceCount;

                options.Add(new KeepOption(dice, score.Score, left));
            }

            return options;
        }

        private static int Pow7(int exponent)
        {
            var value = 1;
            for (var i = 0; i < exponent; i++)
                value *= 7;
            return value;
        }

        private TurnEnd EndTurnWithZonk()
        {
            var player = CurrentPlayer;
            var end = new TurnEnd
            {
                Player = CurrentPlayerIndex,
                IsZonk = true,
                ScoreLost = TurnScore,
            };

            player.ZonkCount++;
            player.ZonkStreak++;

            var penalty = 0;
            foreach (var modifier in _modifiers)
                penalty += modifier.ZonkPenalty(TurnScore);

            if (Rules.ThreeZonkPenalty > 0 && player.ZonkStreak >= 3)
            {
                penalty += Rules.ThreeZonkPenalty;
                player.ZonkStreak = 0;
            }

            penalty = Math.Min(penalty, player.Score);
            player.Score -= penalty;
            end.Penalty = penalty;

            TurnScore = 0;
            AdvanceTurn(end);
            return end;
        }

        private void AdvanceTurn(TurnEnd end)
        {
            CurrentPlayer.TurnsPlayed++;
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Count;
            TurnNumber++;

            if (IsFinalRound && CurrentPlayerIndex == _finalRoundStarter)
            {
                Finish();
                end.MatchFinished = true;
                end.NextPlayer = CurrentPlayerIndex;
                return;
            }

            end.NextPlayer = CurrentPlayerIndex;
            StartTurn();
        }

        private void StartTurn()
        {
            TurnScore = 0;
            for (var die = 0; die < DiceCount; die++)
            {
                _inHand[die] = true;
                _faces[die] = 0;
            }

            Phase = MatchPhase.AwaitingRoll;
        }

        private void Finish()
        {
            Phase = MatchPhase.Finished;
            TurnScore = 0;

            // При равенстве побеждает тот, кто первым дошёл до цели.
            MatchPlayer winner = null;
            foreach (var player in _players)
            {
                if (winner == null || player.Score > winner.Score ||
                    (player.Score == winner.Score && player.Index == _finalRoundStarter))
                {
                    winner = player;
                }
            }

            Winner = winner;
        }
    }
}
