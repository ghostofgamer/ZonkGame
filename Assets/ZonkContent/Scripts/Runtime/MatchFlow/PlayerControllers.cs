using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.Presentation;
using Zonk.UI;

namespace Zonk.MatchFlow
{
    /// <summary>
    /// Кто принимает решения за игрока: человек за экраном, ИИ или (позже) игрок по сети.
    /// Партия не различает их: MatchRunner ждёт решений одинаково.
    /// </summary>
    public interface IPlayerController
    {
        /// <summary>Начало хода: дождаться команды бросить.</summary>
        UniTask WaitRollAsync(ZonkMatch match, CancellationToken ct);

        /// <summary>После броска: какие кости отложить и забрать ли очки.</summary>
        UniTask<TurnDecision> DecideAsync(ZonkMatch match, CancellationToken ct);
    }

    /// <summary>Человек за этим экраном: кнопки HUD и клики по костям.</summary>
    public sealed class LocalPlayerController : IPlayerController
    {
        private readonly MatchHud _hud;
        private readonly DiceSetView _dice;
        private readonly MatchPresenter _presenter;
        private readonly UiKit _kit;

        public LocalPlayerController(MatchHud hud, DiceSetView dice, MatchPresenter presenter, UiKit kit)
        {
            _hud = hud;
            _dice = dice;
            _presenter = presenter;
            _kit = kit;
        }

        public UniTask WaitRollAsync(ZonkMatch match, CancellationToken ct)
        {
            _hud.SetHint(_kit.T("match.hintRoll", match.CurrentPlayer.Name));
            return _hud.WaitRollAsync(ct);
        }

        public async UniTask<TurnDecision> DecideAsync(ZonkMatch match, CancellationToken ct)
        {
            _dice.EnableSelection(match);
            _presenter.HighlightScoringDice(match);

            (bool, bool, int) State()
            {
                var selection = _dice.Selection;
                var score = match.EvaluateSelection(selection);
                if (!score.IsValid)
                    return (false, false, match.TurnScore);

                var remaining = match.DiceInHandCount - selection.Count;
                var canRoll = remaining > 0 || match.Rules.HotDice;
                var total = match.TurnScore + score.Score;
                return (canRoll, match.IsBankAllowed(total), total);
            }

            void OnSelectionChanged(IReadOnlyList<int> selection)
            {
                var score = match.EvaluateSelection(selection);
                if (selection.Count == 0)
                    _hud.SetHint(_kit.T("match.hintSelect"));
                else if (!score.IsValid)
                    _hud.SetHint(_kit.T("match.hintInvalid"));
                else if (!match.IsBankAllowed(match.TurnScore + score.Score) && !match.CurrentPlayer.HasEntered &&
                         match.Rules.EntryScore > 0)
                    _hud.SetHint(_kit.T("match.hintEntry", score.Score, match.Rules.EntryScore));
                else
                    _hud.SetHint(_kit.T("match.hintSelection", score.Score));

                _hud.RefreshActions();
            }

            _dice.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(_dice.Selection);
            try
            {
                var action = await _hud.WaitActionAsync(State, ct);
                return new TurnDecision(new List<int>(_dice.Selection), action == HudAction.Bank);
            }
            finally
            {
                _dice.SelectionChanged -= OnSelectionChanged;
                _dice.DisableSelection();
                _hud.SetHint(string.Empty);
            }
        }
    }

    /// <summary>
    /// ИИ: решает через AiBrain по своему профилю и показывает выбор, откладывая кости по одной.
    /// ГСЧ ИИ свой, отдельный от костей.
    /// </summary>
    public sealed class AiPlayerController : IPlayerController
    {
        private readonly AiProfile _profile;
        private readonly Vector2 _thinkDelay;
        private readonly IRandom _random;
        private readonly DiceSetView _dice;
        private readonly Func<int> _speed;
        private readonly Action _onThinking;

        public AiPlayerController(AiProfile profile, Vector2 thinkDelay, IRandom random, DiceSetView dice, Func<int> speed,
            Action onThinking)
        {
            _profile = profile ?? AiProfile.CreateDefault();
            _thinkDelay = thinkDelay;
            _random = random;
            _dice = dice;
            _speed = speed;
            _onThinking = onThinking;
        }

        public UniTask WaitRollAsync(ZonkMatch match, CancellationToken ct)
        {
            return Delay(0.4f, ct);
        }

        public async UniTask<TurnDecision> DecideAsync(ZonkMatch match, CancellationToken ct)
        {
            var decision = AiBrain.Decide(match, _profile, _random);

            if (_random.NextDouble() < 0.3)
                _onThinking?.Invoke();
            await Delay(Mathf.Lerp(_thinkDelay.x, _thinkDelay.y, (float)_random.NextDouble()), ct);

            foreach (var slot in decision.Keep)
            {
                _dice.Select(slot, true);
                await Delay(0.18f, ct);
            }

            await Delay(0.35f, ct);
            return decision;
        }

        private UniTask Delay(float seconds, CancellationToken ct)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds / Mathf.Max(1, _speed())), cancellationToken: ct);
        }
    }
}
