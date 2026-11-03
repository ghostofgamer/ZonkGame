using System.Collections.Generic;
using System.Threading;
using Base.Platform;
using Base.Services.Monetization;
using Base.Services.Saves;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Dice;
using Zonk.Core.Match;
using Zonk.MatchFlow;
using Zonk.Presentation;
using Zonk.Progress;
using Zonk.UI;
using Zonk.UI.Windows;

namespace Zonk.Table
{
    /// <summary>Общее для режимов после партии: итоги, награды, удвоение за рекламу, межстраничная реклама.</summary>
    public sealed class MatchAftermath
    {
        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly MatchPresenter _presenter;
        private readonly RewardGranter _granter;
        private readonly IRewardService _rewards;
        private readonly IInterstitialService _interstitials;
        private readonly ISaveStore _saves;
        private readonly IUiService _ui;
        private readonly IQuestService _quests;
        private readonly IDieMastery _mastery;
        private readonly IPlayerLevel _level;
        private readonly IPlayerRecords _records;
        private readonly IChestService _chests;
        private readonly ISeasonPass _season;
        private readonly IAchievements _achievements;
        private readonly ITalents _talents;
        private readonly IWallet _wallet;
        private bool _chestEarned;
        private readonly PlayerLevelConfig _levelConfig;
        private readonly CurrencyConfig _coinsCurrency;

        public MatchAftermath(UiKit kit, TableView table, MatchPresenter presenter, RewardGranter granter,
            IRewardService rewards, IInterstitialService interstitials, ISaveStore saves, IUiService ui, IQuestService quests,
            IDieMastery mastery, GameConfig config, IPlayerLevel level, IPlayerRecords records, IChestService chests, ISeasonPass season,
            IAchievements achievements, [Zenject.InjectOptional] ITalents talents, [Zenject.InjectOptional] IWallet wallet)
        {
            _talents = talents;
            _wallet = wallet;
            _achievements = achievements;
            _season = season;
            _chests = chests;
            _records = records;
            _level = level;
            _coinsCurrency = config != null ? config.Coins : null;
            _levelConfig = config != null ? config.PlayerLevel : null;
            _quests = quests;
            _mastery = mastery;
            _kit = kit;
            _table = table;
            _presenter = presenter;
            _granter = granter;
            _rewards = rewards;
            _interstitials = interstitials;
            _saves = saves;
            _ui = ui;
        }

        /// <summary>Показывает итоги. rewards выдаются здесь; удвоение: те же валютные награды ещё раз за рекламу.</summary>
        public async UniTask<ResultsChoice> ShowAsync(MatchOutcome outcome, IReadOnlyList<MatchParticipant> participants,
            IReadOnlyList<Reward> rewards, string interstitialTrigger, bool canAgain, CancellationToken ct,
            IReadOnlyList<GrantedReward> alreadyGranted = null, IReadOnlyList<string> notes = null, int starMask = 0,
            int starCount = 0, string againText = null, string nextGoal = null)
        {
            var match = outcome.Match;
            var winner = outcome.Winner >= 0 ? participants[outcome.Winner] : null;
            var localWon = winner != null && winner.Controller == ControllerKind.Local;
            var vsAi = false;
            foreach (var participant in participants)
                vsAi |= participant.Controller == ControllerKind.Ai;

            string title;
            Color color;
            if (!vsAi && winner != null)
            {
                title = _kit.T("results.playerWon", winner.Name);
                color = UiColors.Gold;
            }
            else
            {
                title = _kit.T(localWon ? "results.won" : outcome.Surrendered ? "results.surrendered" : "results.lost");
                color = localWon ? UiColors.Good : UiColors.Bad;
            }

            await _presenter.ShowResultAsync(localWon || !vsAi, ct);
            // Салют на каждой победе. Талант «Праздник победы» добавляет к нему второй.
            if (localWon && vsAi)
            {
                _presenter.Celebrate();
                if (_talents != null && _talents.Value(TalentEffect.WinCelebration) > 0f)
                    _presenter.Celebrate();
            }

            // Опыт игрока — за любую партию против соперника (за проигрыш тоже), не за игру вдвоём.
            var xp = vsAi ? RecordLocalMatch(outcome, participants, localWon) : default(XpGain?);

            var granted = localWon && rewards != null ? _granter.Grant(rewards) : new List<GrantedReward>();
            if (localWon)
                AddWinBonus(granted, xp.HasValue && xp.Value.FirstWinXp > 0);
            // Уже выданное вне наград режима (выигрыш ставки): показывается, но не удваивается рекламой.
            if (alreadyGranted != null)
                granted.InsertRange(0, alreadyGranted);
            await _saves.SaveNowAsync(ct);

            var canDouble = HasCurrency(rewards) && localWon && _rewards.CanOffer;
            var lost = vsAi && !localWon && !outcome.Surrendered;

            // Поражение: насколько близко было, и кнопка реванша вместо «Ещё раз» (если режим не задал свою).
            var allNotes = new List<string>();
            if (lost && winner != null)
                allNotes.Add(_kit.T("results.short", ScoreGap(match, participants, outcome.Winner)));
            if (notes != null)
                allNotes.AddRange(notes);
            if (lost && string.IsNullOrEmpty(againText))
                againText = _kit.T("results.rematch");

            ResultsChoice choice;
            var screen = await _ui.OpenAsync<ResultsWindow>(ct, w => w.Setup(match, title, color, granted, canDouble, canAgain, allNotes, againText));
            screen.PlayCoinsGained(granted, _coinsCurrency);
            screen.ShowStars(starMask, starCount);
            if (xp.HasValue)
                screen.ShowXp(xp.Value, _level);

            var levelUps = _level != null ? _level.TakeLevelUps() : null;
            RememberUnlocks(levelUps);
            ShowProgressLines(screen);
            if (vsAi)
                screen.SetNextGoal(_kit.T("results.next"), NextGoalText(nextGoal));

            using (var intro = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                PlayIntroAsync(screen, title, color, localWon || !vsAi, levelUps, intro.Token).Forget();
                try
                {
                    while (true)
                    {
                        choice = await screen.WaitChoiceAsync(ct);
                        if (choice != ResultsChoice.DoubleReward)
                            break;

                        var result = await _rewards.RequestAsync("double_reward", ct);
                        screen.HideDouble();
                        if (result.IsGranted())
                        {
                            var extra = _granter.Grant(CurrencyOnly(rewards));
                            granted.AddRange(extra);
                            screen.ShowRewards(granted);
                            _table.Sound.Play(Sfx.Coin);
                        }
                    }
                }
                finally
                {
                    intro.Cancel();
                    await _ui.CloseAsync(screen, CancellationToken.None);
                }
            }

            _presenter.EndMatch();

            // Естественная пауза между партиями: общий слой сам решит, пора ли показывать рекламу.
            if (!string.IsNullOrEmpty(interstitialTrigger))
                await _interstitials.TryShowAsync(interstitialTrigger, ct);

            return choice;
        }

        /// <summary>Режимы, открытые этой партией: меню предложит их попробовать, когда игрок туда вернётся.</summary>
        private readonly List<GameFeature> _pendingUnlocks = new List<GameFeature>();

        public bool TakePendingUnlock(out GameFeature feature)
        {
            feature = default;
            if (_pendingUnlocks.Count == 0)
                return false;

            feature = _pendingUnlocks[0];
            _pendingUnlocks.RemoveAt(0);
            return true;
        }

        public static string ModeKey(GameFeature feature)
        {
            return feature == GameFeature.Tower ? "mode.tower" : "mode.endlessRun";
        }

        private void RememberUnlocks(List<PlayerLevelUp> levelUps)
        {
            if (levelUps == null || _level == null)
                return;

            foreach (var levelUp in levelUps)
            {
                foreach (GameFeature feature in System.Enum.GetValues(typeof(GameFeature)))
                {
                    if (_level.UnlockLevel(feature) == levelUp.Level)
                        _pendingUnlocks.Add(feature);
                }
            }
        }

        private static int ScoreGap(ZonkMatch match, IReadOnlyList<MatchParticipant> participants, int winner)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                if (participants[i].Controller == ControllerKind.Local)
                    return Mathf.Max(0, match.Players[winner].Score - match.Players[i].Score);
            }

            return 0;
        }

        /// <summary>Победа и новые уровни поверх итогов. Кнопки работают сразу, ждать показ не нужно.</summary>
        private async UniTaskVoid PlayIntroAsync(ResultsWindow screen, string title, Color color, bool fire, List<PlayerLevelUp> levelUps,
            CancellationToken ct)
        {
            try
            {
                screen.SetTitleVisible(false);
                await screen.PlayBurstAsync(title, null, color, fire, 0.7f, ct);
                screen.SetTitleVisible(true);
                if (levelUps == null)
                    return;

                foreach (var levelUp in levelUps)
                {
                    _table.Sound.Play(Sfx.Coin);
                    await screen.PlayBurstAsync(_kit.T("level.up", levelUp.Level), RewardNames.Granted(levelUp.Rewards, _kit.T), UiColors.Gold, true, 1.3f, ct);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        /// <summary>Заработанное за партию — строками в итогах, а не надписями по очереди на пустом столе.</summary>
        private void ShowProgressLines(ResultsWindow screen)
        {
            var lines = new List<string>();
            if (_achievements != null)
            {
                _achievements.Check();
                foreach (var unlock in _achievements.TakeUnlocked())
                {
                    var text = _kit.T("achievement.unlocked", RewardNames.AchievementTitle(unlock.Achievement, _kit.T));
                    var rewards = RewardNames.Granted(unlock.Rewards, _kit.T);
                    lines.Add(string.IsNullOrEmpty(rewards) ? text : text + " " + rewards);
                }
            }

            if (_chestEarned)
            {
                _chestEarned = false;
                lines.Add(_kit.T("chest.earned"));
            }

            foreach (var quest in _quests.TakeCompleted())
                lines.Add(_kit.T("quests.completed", quest.Describe(_kit.T)));

            foreach (var levelUp in _mastery.TakeLevelUps())
            {
                var info = _mastery.GetLevelInfo(levelUp.Level);
                lines.Add(_kit.T("mastery.levelUp", _kit.T(levelUp.Die.NameKey), info != null ? _kit.T(info.NameKey) : levelUp.Level.ToString()));
            }

            if (lines.Count > 0)
                _table.Sound.Play(Sfx.Coin);

            // Окно не резиновое: показываем первые, про остальные говорим числом. Всё уже выдано.
            const int visible = 4;
            for (var i = 0; i < lines.Count && i < visible; i++)
                screen.AddLine(lines[i], UiColors.Gold);
            if (lines.Count > visible)
                screen.AddLine(_kit.T("results.more", lines.Count - visible), UiColors.TextMuted);
        }

        /// <summary>Блок «Дальше»: следующий соперник (режим передаёт сам) и ближайшая цель по уровню.</summary>
        private string NextGoalText(string nextGoal)
        {
            var levelGoal = LevelGoalText();
            if (string.IsNullOrEmpty(nextGoal))
                return levelGoal;
            return string.IsNullOrEmpty(levelGoal) ? nextGoal : nextGoal + "\n" + levelGoal;
        }

        private string LevelGoalText()
        {
            var config = _levelConfig;
            if (config == null || _level == null)
                return null;

            var current = _level.Level;
            if (config.MaxLevel > 0 && current >= config.MaxLevel)
                return null;

            // Ближайший рубеж с наградой и ближайшее открытие режима: что раньше, то и цель.
            PlayerLevelMilestone milestone = null;
            foreach (var entry in config.Milestones)
            {
                if (entry != null && entry.Level > current && entry.Rewards.Count > 0 && (milestone == null || entry.Level < milestone.Level))
                    milestone = entry;
            }

            var unlockLevel = int.MaxValue;
            var unlockFeature = default(GameFeature);
            foreach (GameFeature feature in System.Enum.GetValues(typeof(GameFeature)))
            {
                var at = _level.UnlockLevel(feature);
                if (at > current && at < unlockLevel)
                {
                    unlockLevel = at;
                    unlockFeature = feature;
                }
            }

            long Left(int level) => System.Math.Max(0L, Zonk.Core.Progress.PlayerLevelMath.TotalXpFor(level, config.FirstLevelXp, config.LevelXpGrowth) - _level.Xp);

            if (unlockLevel != int.MaxValue && (milestone == null || unlockLevel <= milestone.Level))
                return _kit.T("results.nextMode", Left(unlockLevel), unlockLevel, _kit.T(ModeKey(unlockFeature)));
            if (milestone != null)
                return _kit.T("results.nextReward", Left(milestone.Level), milestone.Level, RewardNames.List(milestone.Rewards, _kit.T));
            return _kit.T("results.nextLevel", Left(current + 1), current + 1);
        }

        /// <summary>Партия местного игрока против соперника: статистика и опыт (победа или проигрыш, очки, босс).</summary>
        private XpGain? RecordLocalMatch(MatchOutcome outcome, IReadOnlyList<MatchParticipant> participants, bool localWon)
        {
            var local = -1;
            var vsBoss = false;
            for (var i = 0; i < participants.Count; i++)
            {
                if (participants[i].Controller == ControllerKind.Local && local < 0)
                    local = i;
                if (participants[i].Opponent != null && participants[i].Opponent.IsBoss)
                    vsBoss = true;
            }

            if (local < 0)
                return null;

            if (_records != null)
                _records.RecordMatch(outcome.Mode, localWon, outcome.Surrendered, vsBoss, participants[local].Dice, outcome.Seconds);
            if (localWon && _chests != null && _chests.AddWin())
                _chestEarned = true;

            var score = outcome.Match != null && local < outcome.Match.Players.Count ? outcome.Match.Players[local].Score : 0;
            var xp = _level != null ? _level.AddMatch(localWon, outcome.Surrendered, score, vsBoss) : (XpGain?)null;
            // Очки сезона — тот же опыт.
            if (xp.HasValue && _season != null)
                _season.AddPoints(xp.Value.Total);
            return xp;
        }

        /// <summary>
        /// Таланты «Купца»: +CoinsOnWinPercent к монетам за победу и ×2 за первую победу дня (FirstWinDoubleCoins).
        /// Считается от монет наград режима; ставка и удвоение за рекламу — без бонуса.
        /// </summary>
        private void AddWinBonus(List<GrantedReward> granted, bool firstWinToday)
        {
            if (_talents == null || _wallet == null || _coinsCurrency == null)
                return;

            var coins = 0;
            foreach (var reward in granted)
            {
                if (reward.Currency == _coinsCurrency)
                    coins += reward.Amount;
            }

            var percent = _talents.Value(TalentEffect.CoinsOnWinPercent);
            if (firstWinToday && _talents.Value(TalentEffect.FirstWinDoubleCoins) > 0f)
                percent += 100f;
            var bonus = Mathf.RoundToInt(coins * percent / 100f);
            if (bonus <= 0)
                return;

            _wallet.Add(_coinsCurrency, bonus);
            granted.Add(new GrantedReward(null, _coinsCurrency, bonus));
        }

        private static bool HasCurrency(IReadOnlyList<Reward> rewards)
        {
            if (rewards == null)
                return false;
            foreach (var reward in rewards)
            {
                if (reward is CurrencyReward)
                    return true;
            }

            return false;
        }

        private static IEnumerable<Reward> CurrencyOnly(IReadOnlyList<Reward> rewards)
        {
            foreach (var reward in rewards)
            {
                if (reward is CurrencyReward)
                    yield return reward;
            }
        }
    }

    /// <summary>Игра вдвоём на одном экране: бесплатно, без наград.</summary>
    public sealed class HotSeatState : ITableState
    {
        private readonly IQuestService _quests;
        private readonly HotSeatSettings _settings;
        private readonly GameConfig _config;
        private readonly ContentDatabase _content;
        private readonly IInventory _inventory;
        private readonly OwnedContent _owned;
        private readonly ParticipantFactory _participants;
        private readonly MatchRunner _runner;
        private readonly MatchAftermath _aftermath;
        private readonly IPlatformService _platform;
        private readonly IUiService _ui;

        public HotSeatState(HotSeatSettings settings, GameConfig config, ContentDatabase content,
            IInventory inventory, OwnedContent owned, ParticipantFactory participants, MatchRunner runner,
            MatchAftermath aftermath, IPlatformService platform, IUiService ui, IQuestService quests)
        {
            _quests = quests;
            _settings = settings;
            _config = config;
            _content = content;
            _inventory = inventory;
            _owned = owned;
            _participants = participants;
            _runner = runner;
            _aftermath = aftermath;
            _platform = platform;
            _ui = ui;
        }

        public string Id => TableStateIds.HotSeat;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            var setup = await _ui.OpenAsync<HotSeatSetupWindow>(ct, w => w.Setup(_settings, _config, _content, _inventory,
                _owned.Dice(), _owned.Items(SlotIds.DiceSkin)));
            bool start;
            try
            {
                start = await setup.WaitStartAsync(ct);
            }
            finally
            {
                await _ui.CloseAsync(setup, CancellationToken.None);
            }

            if (!start)
                return TableStateIds.Menu;

            while (true)
            {
                var data = _settings.Data;
                var players = new List<MatchParticipant>();
                for (var i = 0; i < HotSeatSettings.PlayerCount; i++)
                    players.Add(_participants.HotSeatPlayer(i, data.Players[i], data.SpecialDice));

                // Одинаковые скины: второму игроку запасной, чтобы кости не путались.
                if (players[0].DiceSkin == players[1].DiceSkin && _config.SecondPlayerFallbackSkin != null)
                    players[1].DiceSkin = _config.SecondPlayerFallbackSkin;

                var mode = _config.HotSeatMode;
                var settings = new MatchSettings
                {
                    Rules = mode != null && mode.Rules != null ? mode.Rules.ToRuleSet(data.Target) : Core.Rules.RuleSet.CreateClassic(),
                    Players = new List<PlayerSetup>
                    {
                        new PlayerSetup(players[0].Name, ParticipantFactory.Specs(players[0].Dice)),
                        new PlayerSetup(players[1].Name, ParticipantFactory.Specs(players[1].Dice)),
                    },
                    Seed = SplitMixRandom.NewSeed(),
                    FirstPlayer = _settings.PickFirstPlayer(),
                };
                settings.Rules.TargetScore = data.Target;

                _platform.NotifyGameplayStart();
                MatchOutcome outcome;
                try
                {
                    outcome = await _runner.RunAsync(settings, players, ct);
                }
                finally
                {
                    _platform.NotifyGameplayStop();
                }

                _quests.Report(new QuestEvent
                {
                    Kind = QuestEventKind.MatchFinished,
                    HotSeat = true,
                    Won = outcome.Winner >= 0,
                    Surrendered = outcome.Surrendered,
                    Dice = outcome.Winner >= 0 ? players[outcome.Winner].Dice : null,
                });

                var choice = await _aftermath.ShowAsync(outcome, players, mode != null ? mode.WinRewards : null,
                    mode != null ? mode.InterstitialTrigger : null, true, ct);
                if (choice != ResultsChoice.Again)
                    return TableStateIds.Menu;
            }
        }
    }

    /// <summary>
    /// Кампания: выбор соперника, сюжет главы, энергия за вход, партия, награды,
    /// концовка главы после босса.
    /// </summary>
    public sealed class CampaignState : ITableState
    {
        private readonly RewardGranter _granter;
        private readonly PlayerStats _stats;
        private readonly UiKit _kit;
        private readonly TableView _table;
        private readonly ICampaignProgress _progress;
        private readonly IWallet _wallet;
        private readonly GameConfig _config;
        private readonly ILoadout _loadout;
        private readonly OwnedContent _owned;
        private readonly ModeMatch _match;
        private readonly MatchAftermath _aftermath;
        private readonly StageDresser _dresser;
        private readonly EnergyGate _energy;
        private readonly ITalents _talents;
        private readonly IUiService _ui;

        public CampaignState(UiKit kit, TableView table, ICampaignProgress progress, IWallet wallet, GameConfig config,
            ILoadout loadout, OwnedContent owned, ModeMatch match, MatchAftermath aftermath, StageDresser dresser,
            IUiService ui, RewardGranter granter, PlayerStats stats, EnergyGate energy,
            [Zenject.InjectOptional] ITalents talents)
        {
            _talents = talents;
            _energy = energy;
            _stats = stats;
            _granter = granter;
            _kit = kit;
            _table = table;
            _progress = progress;
            _wallet = wallet;
            _config = config;
            _loadout = loadout;
            _owned = owned;
            _match = match;
            _aftermath = aftermath;
            _dresser = dresser;
            _ui = ui;
        }

        public string Id => TableStateIds.Campaign;

        public async UniTask<string> RunAsync(CancellationToken ct)
        {
            while (true)
            {
                OpponentConfig opponent;
                ChapterConfig chapter;
                int stake;
                var editDice = false;
                var allowSpecial = _config.CampaignMode == null || _config.CampaignMode.AllowSpecialDice;
                var screen = await _ui.OpenAsync<CampaignWindow>(ct,
                    w => w.Setup(_progress, _config, _loadout, _owned.Dice(allowSpecial)));
                try
                {
                    opponent = await screen.WaitChoiceAsync(ct);
                    chapter = screen.Chapter;
                    stake = screen.Stake;
                    editDice = screen.EditDiceRequested;
                }
                finally
                {
                    await _ui.CloseAsync(screen, CancellationToken.None);
                }

                // «Изменить» кости: «Мои кости» на столе, затем снова кампания.
                if (editDice)
                {
                    await DiceWindow.EditAsync(_ui, ct);
                    continue;
                }

                if (opponent == null)
                    return TableStateIds.Menu;

                // Закрытого соперника (в том числе грозного без звёзд у босса) не начинаем, даже если кнопка сработала.
                if (_progress.GetState(chapter, opponent) == OpponentState.Locked)
                    continue;

                if (!_progress.IsIntroSeen(chapter) && chapter.Intro.Count > 0)
                {
                    await StoryWindow.ShowAsync(_ui, chapter.Intro, ct);
                    _progress.MarkIntroSeen(chapter);
                }

                if (!await PayEnergyAsync(opponent, ct))
                    continue;

                while (await PlayAsync(chapter, opponent, stake, ct) && await PayEnergyAsync(opponent, ct))
                {
                }
            }
        }

        /// <summary>Партия с соперником. true: игрок хочет ещё раз с тем же соперником.</summary>
        private async UniTask<bool> PlayAsync(ChapterConfig chapter, OpponentConfig opponent, int stake, CancellationToken ct)
        {
            // Ставка списывается до партии; не хватает монет (потратил между партиями) — играем без ставки.
            if (stake > 0 && (_config.Coins == null || !_wallet.TrySpend(_config.Coins, stake)))
            {
                await Toast.ShowAsync(_kit, _table.UiRoot, _kit.T("campaign.stakeNoCoins"), UiColors.Bad, 1f, ct);
                stake = 0;
            }

            // Цель — у соперника своя или режима; правила соперника — на обоих (ModeMatch: участники, партия, отчёт заданиям).
            var mode = _config.CampaignMode;
            ModeMatchResult result;
            try
            {
                // Локация главы — до партии: стаканы и свет партии уже в ней.
                await _dresser.OverrideAsync(chapter.Environment, ct);
                result = await _match.PlayAsync(new ModeMatchSetup
                {
                    Mode = TableStateIds.Campaign,
                    Opponent = opponent,
                    Target = opponent.TargetScore,
                    SharedRules = opponent.Modifiers,
                }, ct);
            }
            catch (System.Exception e) when (!(e is System.OperationCanceledException && ct.IsCancellationRequested))
            {
                // Партия сорвалась не по вине игрока: ставка и энергия возвращаются. Дальше TableFlow вернёт в меню,
                // меню уберёт стол и вернёт локацию игрока.
                if (stake > 0)
                    _wallet.Add(_config.Coins, stake);
                await _energy.RefundAfterErrorAsync(CampaignWindow.EnergyCost(opponent, _config), ct);
                throw;
            }

            var outcome = result.Outcome;
            var players = result.Players;
            var me = players[0];
            var won = result.Won;
            var firstWin = won && _progress.MarkBeaten(opponent);
            var rewards = new List<Reward>();
            if (won)
            {
                rewards.AddRange(firstWin ? opponent.FirstWinRewards : opponent.RepeatWinRewards);
                if (mode != null)
                    rewards.AddRange(mode.WinRewards);
            }

            _stats.RecordCampaignMatch(won, outcome.Match.Players[0].BestTurn);

            var stakeRewards = new List<GrantedReward>();
            var notes = new List<string>();
            if (won)
                AwardStars(opponent, outcome, me, stakeRewards, notes);

            if (stake > 0 && won)
            {
                var payout = stake + CampaignWindow.StakeWin(opponent, stake);
                _wallet.Add(_config.Coins, payout);
                stakeRewards.Add(new GrantedReward(null, _config.Coins, payout));
            }
            else if (stake > 0)
            {
                Toast.ShowAsync(_kit, _table.UiRoot, _kit.T("campaign.stakeLost", stake), UiColors.Bad, 1.2f, ct).Forget();
                // Талант «Страховка ставки»: часть ставки возвращается.
                var refundPercent = _talents != null ? Mathf.Clamp(_talents.Value(TalentEffect.StakeRefundPercent), 0f, 100f) : 0f;
                var refund = Mathf.FloorToInt(stake * refundPercent / 100f);
                if (refund > 0)
                {
                    _wallet.Add(_config.Coins, refund);
                    stakeRewards.Add(new GrantedReward(null, _config.Coins, refund));
                }
            }

            var choice = await _aftermath.ShowAsync(outcome, players, rewards, mode != null ? mode.InterstitialTrigger : null,
                true, ct, stakeRewards, notes, won ? _progress.GetStarMask(opponent) : 0, won ? StarsText.MaxStars(opponent) : 0,
                nextGoal: won ? NextOpponentText(chapter) : null);

            _dresser.ApplyEquipped();

            if (firstWin && opponent.IsBoss && !_progress.IsOutroSeen(chapter) && chapter.Outro.Count > 0)
            {
                await StoryWindow.ShowAsync(_ui, chapter.Outro, ct);
                _progress.MarkOutroSeen(chapter);
            }

            return choice == ResultsChoice.Again;
        }

        /// <summary>Следующий соперник главы, которого ещё не победили; все побеждены — пусто.</summary>
        private string NextOpponentText(ChapterConfig chapter)
        {
            foreach (var opponent in chapter.Opponents)
            {
                if (opponent != null && _progress.GetState(chapter, opponent) == OpponentState.Available)
                    return _kit.T("results.nextOpponent", _kit.T(opponent.NameKey));
            }

            return null;
        }

        /// <summary>
        /// Звёзды за победу: первая — сама победа, остальные — условия соперника. Новые звёзды дают награду
        /// (GameConfig.NewStarRewards), в итогах — строка звёзд и какие условия выполнены впервые.
        /// </summary>
        private void AwardStars(OpponentConfig opponent, MatchOutcome outcome, MatchParticipant me, List<GrantedReward> granted,
            List<string> notes)
        {
            var context = new StarContext(outcome.Match, 0, me.Dice);
            var mask = 1;
            for (var i = 0; i < opponent.StarConditions.Count; i++)
            {
                var condition = opponent.StarConditions[i];
                if (condition != null && condition.IsMet(context))
                    mask |= 1 << (i + 1);
            }

            var gained = _progress.AddStars(opponent, mask);

            for (var bit = 0; bit < StarsText.MaxStars(opponent); bit++)
            {
                if ((gained & (1 << bit)) == 0)
                    continue;

                var text = bit == 0 ? _kit.T("star.win") : StarsText.Condition(opponent.StarConditions[bit - 1], _kit.T);
                notes.Add(_kit.T("results.newStar", text));
                granted.AddRange(_granter.Grant(_config.NewStarRewards));
            }
        }

        /// <summary>Списывает энергию за партию. Не хватает: предложить пополнение за рекламу (EnergyGate).</summary>
        private UniTask<bool> PayEnergyAsync(OpponentConfig opponent, CancellationToken ct)
        {
            return _energy.PayAsync(CampaignWindow.EnergyCost(opponent, _config), ct);
        }
    }
}
