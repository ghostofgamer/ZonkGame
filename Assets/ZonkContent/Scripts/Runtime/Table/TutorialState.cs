using System.Threading;
using Cysharp.Threading.Tasks;
using Zonk.Configs;
using Zonk.UI;

namespace Zonk.Table
{
    /// <summary>
    /// Партия обучения: обычные кости, короткая цель, заданные броски (TutorialConfig), игрок ходит первым.
    /// Без энергии, ставки и наград: цель — научиться. Сыграна до конца — меню открывается целиком.
    /// Сдался или вышел — снова меню с одной кнопкой: шаги, что уже были, не повторяются.
    /// </summary>
    public sealed class TutorialState : ITableState
    {
        private readonly ModeMatch _match;
        private readonly TutorialDirector _tutorial;
        private readonly GameConfig _config;
        private readonly IUiService _ui;
        private readonly UiKit _kit;

        public TutorialState(ModeMatch match, TutorialDirector tutorial, GameConfig config, IUiService ui, UiKit kit)
        {
            _match = match;
            _tutorial = tutorial;
            _config = config;
            _ui = ui;
            _kit = kit;
        }

        public string Id => TableStateIds.Tutorial;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            var tutorial = _config.Tutorial;
            ModeMatchResult result;
            _tutorial.Guided = true;
            try
            {
                result = await _match.PlayAsync(new ModeMatchSetup
                {
                    Mode = TableStateIds.Tutorial,
                    Opponent = tutorial.Opponent,
                    Target = tutorial.TargetScore,
                    PlayerDice = new DieConfig[Core.Match.ZonkMatch.DiceCount],
                    PlayerFirst = true,
                    PlayerRolls = TutorialConfig.Faces(tutorial.PlayerRolls),
                    OpponentRolls = TutorialConfig.Faces(tutorial.OpponentRolls),
                }, ct);
            }
            finally
            {
                _tutorial.Guided = false;
            }

            if (result.Outcome.Surrendered)
                return TableStateIds.Menu;

            _tutorial.CompleteFirstMatch();
            await ModeWindows.AskAsync(_ui, _kit.T("tutorial.doneTitle"), null, _kit.T("tutorial.doneBody"), _kit.T("tutorial.doneButton"), null, null, ct);
            return TableStateIds.Menu;
        }
    }
}
