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

        // Рабочие буферы подсчёта: EvaluateSelection и GetKeepOptions вызываются на каждый бросок и выбор кости
        // (и десятки раз за решение ИИ), поэтому без новых списков и массивов на каждый вызов.
        private readonly int[] _countsBuffer = new int[7];
        private readonly bool[] _seenBuffer = new bool[DiceCount];
        private readonly List<int> _handBuffer = new List<int>(DiceCount);
        private readonly HashSet<int> _keysBuffer = new HashSet<int>();
        private readonly bool[] _inHand = new bool[DiceCount];
        private readonly List<KeepOption> _sharedOptions = new List<KeepOption>();
        private readonly List<KeepOption> _optionPool = new List<KeepOption>();
        private readonly List<MatchModifier> _modifiers;
        private readonly ScoreCalculator[] _calculators;
        private readonly IReadOnlyList<MatchModifier>[] _playerModifiers;
        private int _finalRoundStarter = -1;

        public ZonkMatch(MatchSettings settings)
        {
            if (settings.Players.Count < 2)
                throw new ArgumentException("A match needs at least two players");

            _modifiers = new List<MatchModifier>(settings.Modifiers);
            Rules = settings.Rules.Clone();
            foreach (var modifier in _modifiers)
                modifier.ModifyRules(Rules);

            // Расчёт очков у каждого игрока свой: общие правила плюс его личные (находки). Без личных — общий расчёт.
            var shared = new ScoreCalculator(Rules.Rules, _modifiers);
            _calculators = new ScoreCalculator[settings.Players.Count];
            _playerModifiers = new IReadOnlyList<MatchModifier>[settings.Players.Count];
            for (var i = 0; i < settings.Players.Count; i++)
            {
                var own = settings.Players[i].Modifiers ?? Array.Empty<MatchModifier>();
                _playerModifiers[i] = own;
                if (own.Count == 0)
                {
                    _calculators[i] = shared;
                    continue;
                }

                var combined = new List<MatchModifier>(_modifiers);
                combined.AddRange(own);
                _calculators[i] = new ScoreCalculator(Rules.Rules, combined);
            }

            _random = new SplitMixRandom(settings.Seed);

            for (var i = 0; i < settings.Players.Count; i++)
            {
                var setup = settings.Players[i];
                if (setup.Dice == null || setup.Dice.Count != DiceCount)
                    throw new ArgumentException($"Player {i} must have exactly {DiceCount} dice");
                _players.Add(new MatchPlayer(i, setup));
            }

            var first = settings.FirstPlayer;
            for (var i = 0; i < _modifiers.Count; i++)
                first = _modifiers[i].ChooseFirstPlayer(first, _players.Count);
            CurrentPlayerIndex = Math.Max(0, Math.Min(first, _players.Count - 1));
            StartTurn();
        }

        /// <summary>Правила с учётом модификаторов.</summary>
        public RuleSet Rules { get; }

        /// <summary>Расчёт очков текущего игрока: общие правила и его личные.</summary>
        public ScoreCalculator Calculator => _calculators[CurrentPlayerIndex];

        /// <summary>Личные правила игрока (находки).</summary>
        public IReadOnlyList<MatchModifier> PlayerModifiers(int player) => _playerModifiers[player];

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

            // Список брошенных и копия граней уходят в итог (визуал держит их, пока проигрывает бросок),
            // поэтому свои; для проверки на Зонк — общий рабочий массив.
            var rolled = new List<int>(DiceCount);
            var counts = _countsBuffer;
            Array.Clear(counts, 0, counts.Length);
            var dice = CurrentPlayer.Dice;
            for (var die = 0; die < DiceCount; die++)
            {
                if (!_inHand[die])
                    continue;

                var face = dice[die].Roll(_random);
                _faces[die] = face;
                rolled.Add(die);
                counts[face]++;
            }

            var outcome = new RollOutcome
            {
                Player = CurrentPlayerIndex,
                RolledDice = rolled,
                Faces = (int[])_faces.Clone(),
            };

            if (Calculator.HasAnyScoreCounts(counts))
            {
                Phase = MatchPhase.AwaitingKeep;
                return outcome;
            }

            // Заряд спасения: ход не кончается, очки хода целы, те же кости бросаются снова.
            if (CurrentPlayer.ZonkSavesLeft > 0)
            {
                CurrentPlayer.ZonkSavesLeft--;
                outcome.ZonkSaved = true;
                Phase = MatchPhase.AwaitingRoll;
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

            var counts = _countsBuffer;
            var seen = _seenBuffer;
            Array.Clear(counts, 0, counts.Length);
            Array.Clear(seen, 0, seen.Length);
            for (var i = 0; i < dice.Count; i++)
            {
                var die = dice[i];
                if (die < 0 || die >= DiceCount || seen[die] || !_inHand[die] || _faces[die] == 0)
                    return ScoreResult.Invalid;

                seen[die] = true;
                counts[_faces[die]]++;
            }

            return Calculator.EvaluateCounts(counts);
        }

        public KeepOutcome Keep(IReadOnlyList<int> dice)
        {
            if (Phase != MatchPhase.AwaitingKeep)
                throw new InvalidOperationException($"Cannot keep in phase {Phase}");

            var score = EvaluateSelection(dice);
            if (!score.IsValid)
                throw new InvalidOperationException("Selected dice do not score");

            for (var i = 0; i < dice.Count; i++)
                _inHand[dice[i]] = false;

            TurnScore += score.Score;

            var hotDice = false;
            var bonus = 0;
            if (DiceInHandCount == 0 && Rules.HotDice)
            {
                hotDice = true;
                bonus = SumHotDiceBonus(score.Score);
                TurnScore += bonus;
                CurrentPlayer.HotDiceCount++;
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
                Bonus = bonus,
            };
        }

        /// <summary>Можно ли бросить ещё: в руке есть кости.</summary>
        public bool CanRollAgain => Phase == MatchPhase.AwaitingDecision && DiceInHandCount > 0;

        public TurnEnd Bank()
        {
            if (!CanBank)
                throw new InvalidOperationException("Cannot bank now");

            var player = CurrentPlayer;
            var bonus = SumBankBonus(TurnScore);
            var banked = TurnScore + bonus;
            player.Score += banked;
            player.HasEntered = true;
            player.ZonkStreak = 0;
            player.BestTurn = Math.Max(player.BestTurn, banked);

            var end = new TurnEnd { Player = CurrentPlayerIndex, Banked = banked, Bonus = bonus };

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
            CollectKeepOptions(options, false);
            return options;
        }

        /// <summary>
        /// Те же варианты в общем буфере партии, без новых объектов. Список и варианты действительны только
        /// до следующего вызова: кто хранит выбор дольше (решение ИИ), копирует кости.
        /// </summary>
        internal IReadOnlyList<KeepOption> GetKeepOptionsShared()
        {
            _sharedOptions.Clear();
            CollectKeepOptions(_sharedOptions, true);
            return _sharedOptions;
        }

        /// <summary>Отмечает кости, которые входят хотя бы в один вариант отложить (подсказка). Без новых объектов.</summary>
        public void MarkScoringDice(bool[] scoring)
        {
            Array.Clear(scoring, 0, scoring.Length);
            var options = GetKeepOptionsShared();
            for (var i = 0; i < options.Count; i++)
            {
                var dice = options[i].Dice;
                for (var d = 0; d < dice.Count; d++)
                    scoring[dice[d]] = true;
            }
        }

        private void CollectKeepOptions(List<KeepOption> options, bool reuse)
        {
            if (Phase != MatchPhase.AwaitingKeep)
                return;

            var hand = _handBuffer;
            hand.Clear();
            for (var die = 0; die < DiceCount; die++)
            {
                if (_inHand[die] && _faces[die] > 0)
                    hand.Add(die);
            }

            // Набор граней считается один раз (ключ — число костей каждой грани), список костей создаётся
            // только у вариантов, которые приносят очки.
            var seen = _keysBuffer;
            seen.Clear();
            var counts = _countsBuffer;
            var subsetCount = 1 << hand.Count;
            for (var mask = 1; mask < subsetCount; mask++)
            {
                Array.Clear(counts, 0, counts.Length);
                var key = 0;
                var size = 0;
                for (var bit = 0; bit < hand.Count; bit++)
                {
                    if ((mask & (1 << bit)) == 0)
                        continue;
                    var face = _faces[hand[bit]];
                    counts[face]++;
                    key += Pow7(face - 1);
                    size++;
                }

                if (!seen.Add(key))
                    continue;

                var score = Calculator.EvaluateCounts(counts);
                if (!score.IsValid)
                    continue;

                var left = hand.Count - size;
                if (left == 0 && Rules.HotDice)
                    left = DiceCount;

                List<int> dice;
                if (reuse)
                {
                    if (options.Count == _optionPool.Count)
                        _optionPool.Add(new KeepOption());
                    var option = _optionPool[options.Count];
                    dice = option.Reuse(score.Score, left);
                    options.Add(option);
                }
                else
                {
                    dice = new List<int>(size);
                    options.Add(new KeepOption(dice, score.Score, left));
                }

                for (var bit = 0; bit < hand.Count; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                        dice.Add(hand[bit]);
                }
            }
        }

        /// <summary>Добавка за горячие кости: общие правила и личные текущего игрока. Без новых объектов.</summary>
        private int SumHotDiceBonus(int keepScore)
        {
            var bonus = 0;
            for (var i = 0; i < _modifiers.Count; i++)
                bonus += _modifiers[i].HotDiceBonus(keepScore);
            var own = _playerModifiers[CurrentPlayerIndex];
            for (var i = 0; i < own.Count; i++)
                bonus += own[i].HotDiceBonus(keepScore);
            return Math.Max(0, bonus);
        }

        /// <summary>Добавка к записанным очкам за длинный ход.</summary>
        private int SumBankBonus(int turnScore)
        {
            var bonus = 0;
            for (var i = 0; i < _modifiers.Count; i++)
                bonus += _modifiers[i].BankBonus(turnScore);
            var own = _playerModifiers[CurrentPlayerIndex];
            for (var i = 0; i < own.Count; i++)
                bonus += own[i].BankBonus(turnScore);
            return Math.Max(0, bonus);
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
            var own = _playerModifiers[CurrentPlayerIndex];
            for (var i = 0; i < own.Count; i++)
                penalty += own[i].ZonkPenalty(TurnScore);

            if (Rules.ThreeZonkPenalty > 0 && player.ZonkStreak >= 3)
            {
                penalty += Rules.ThreeZonkPenalty;
                player.ZonkStreak = 0;
            }

            // Страховка: часть сгоревших очков хода остаётся на счету.
            var saved = 0;
            for (var i = 0; i < _modifiers.Count; i++)
                saved += _modifiers[i].ZonkKeep(TurnScore);
            for (var i = 0; i < own.Count; i++)
                saved += own[i].ZonkKeep(TurnScore);
            saved = Math.Max(0, Math.Min(saved, TurnScore));
            player.Score += saved;
            end.Saved = saved;

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
