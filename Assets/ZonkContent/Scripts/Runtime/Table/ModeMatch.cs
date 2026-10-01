using System.Collections.Generic;
using System.Threading;
using Base.Platform;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.Core.Modifiers;
using Zonk.MatchFlow;
using Zonk.Progress;
using Zonk.UI;

namespace Zonk.Table
{
    /// <summary>Что нужно для партии режима-испытания.</summary>
    public sealed class ModeMatchSetup
    {
        public OpponentConfig Opponent;

        /// <summary>Кости врага по слотам (null в списке — обычная). Список null — кости соперника из его конфига.</summary>
        public IReadOnlyList<DieConfig> EnemyDice;

        /// <summary>ИИ врага; null — ИИ соперника из его конфига.</summary>
        public AiProfileConfig EnemyAi;

        /// <summary>Кости игрока по слотам (null в списке — обычная). Список null — набор игрока из экипировки.</summary>
        public IReadOnlyList<DieConfig> PlayerDice;

        public int Target;
        public IReadOnlyList<MatchModifier> SharedRules;
        public IReadOnlyList<MatchModifier> PlayerModifiers;
        public IReadOnlyList<MatchModifier> EnemyModifiers;
    }

    /// <summary>Итог партии режима: для итогов (MatchAftermath) и решения режима.</summary>
    public sealed class ModeMatchResult
    {
        public MatchOutcome Outcome;
        public List<MatchParticipant> Players;
        public bool Won;
    }

    /// <summary>
    /// Партия режима-испытания (башня, «Бесконечный забег») против одного соперника: участники с костями режима,
    /// общие и личные правила, цель, партия, отчёт для заданий. Итоги и награды показывает режим (MatchAftermath).
    /// </summary>
    public sealed class ModeMatch
    {
        private readonly UiKit _kit;
        private readonly ParticipantFactory _participants;
        private readonly MatchRunner _runner;
        private readonly IPlatformService _platform;
        private readonly IQuestService _quests;
        private readonly GameConfig _config;

        public ModeMatch(UiKit kit, ParticipantFactory participants, MatchRunner runner, IPlatformService platform,
            IQuestService quests, GameConfig config)
        {
            _kit = kit;
            _participants = participants;
            _runner = runner;
            _platform = platform;
            _quests = quests;
            _config = config;
        }

        public async UniTask<ModeMatchResult> PlayAsync(ModeMatchSetup setup, CancellationToken ct)
        {
            var mode = _config.CampaignMode;
            var allowSpecial = mode == null || mode.AllowSpecialDice;

            var me = _participants.LocalPlayer(_kit.T("campaign.you"), allowSpecial);
            if (setup.PlayerDice != null)
            {
                me.Dice = Resolve(setup.PlayerDice);
                me.MasteryLevels = _participants.MasteryLevels(me.Dice);
            }

            var enemy = _participants.Opponent(setup.Opponent, _kit.T(setup.Opponent.NameKey));
            if (setup.EnemyDice != null)
                enemy.Dice = Resolve(setup.EnemyDice);
            if (setup.EnemyAi != null)
            {
                enemy.AiProfile = setup.EnemyAi.ToProfile();
                enemy.AiThinkDelay = setup.EnemyAi.ThinkDelay;
            }

            var players = new List<MatchParticipant> { me, enemy };
            var rules = mode != null && mode.Rules != null ? mode.Rules.ToRuleSet(setup.Target) : Core.Rules.RuleSet.CreateClassic();
            if (setup.Target > 0)
                rules.TargetScore = setup.Target;

            var settings = new MatchSettings
            {
                Rules = rules,
                Players = new List<PlayerSetup>
                {
                    new PlayerSetup(me.Name, ParticipantFactory.Specs(me.Dice)) { Modifiers = List(setup.PlayerModifiers) },
                    new PlayerSetup(enemy.Name, ParticipantFactory.Specs(enemy.Dice)) { Modifiers = List(setup.EnemyModifiers) },
                },
                Modifiers = new List<MatchModifier>(List(setup.SharedRules)),
                Seed = SplitMixRandom.NewSeed(),
                FirstPlayer = Random.Range(0, 2),
            };

            _platform.NotifyGameplayStart();
            var outcome = await _runner.RunAsync(settings, players, ct);
            _platform.NotifyGameplayStop();

            var won = outcome.Winner == 0 && !outcome.Surrendered;
            _quests.Report(new QuestEvent
            {
                Kind = QuestEventKind.MatchFinished,
                Won = won,
                Surrendered = outcome.Surrendered,
                VsBoss = setup.Opponent.IsBoss,
                Dice = me.Dice,
            });

            return new ModeMatchResult { Outcome = outcome, Players = players, Won = won };
        }

        /// <summary>Кости по слотам: null — обычная; ровно шесть.</summary>
        private List<DieConfig> Resolve(IReadOnlyList<DieConfig> dice)
        {
            var result = new List<DieConfig>(ZonkMatch.DiceCount);
            for (var i = 0; i < ZonkMatch.DiceCount; i++)
            {
                var die = i < dice.Count ? dice[i] : null;
                result.Add(die != null ? die : _config.StandardDie);
            }

            return result;
        }

        private static IReadOnlyList<MatchModifier> List(IReadOnlyList<MatchModifier> modifiers)
        {
            return modifiers ?? System.Array.Empty<MatchModifier>();
        }
    }
}
