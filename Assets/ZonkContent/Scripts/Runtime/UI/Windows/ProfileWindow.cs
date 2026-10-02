using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Core.Progress;
using Zonk.Progress;
using Zonk.Table;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Профиль игрока, две вкладки. «Статистика»: разделы (общее, режимы, броски, комбинации, удача, кости), строки —
    /// копии детали Parts/StatRow. «Облик»: аватары и рамки (ячейки Parts/AppearanceCell) — открытые цветом редкости,
    /// не открытые серые; нажатие на открытый надевает, на закрытый — примерка и подсказка, где взять.
    /// Списки строятся при открытии (редко: память выделять можно). Открывается нажатием на уровень или аватар
    /// в главном меню, закрывается кнопкой «Назад» (WaitCloseRequestAsync). Без вкладок (старый префаб) — только статистика.
    /// </summary>
    public sealed partial class ProfileWindow : UiWindow
    {
        /// <summary>Сколько честных бросков нужно, чтобы говорить об удаче.</summary>
        private const int MinLuckRolls = 30;

        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _list;
        [SerializeField] private StatRowView _rowTemplate;
        [SerializeField] private UiButtonView _back;

        [Header("Вкладки (необязательно)")]
        [SerializeField] private UiButtonView _statsTab;
        [SerializeField] private UiButtonView _lookTab;
        [SerializeField] private GameObject _statsPage;
        [SerializeField] private GameObject _lookPage;

        [Header("Облик")]
        [SerializeField] private PlayerAvatarView _preview;
        [SerializeField] private TMP_Text _lookInfo;
        [SerializeField] private TMP_Text _avatarsTitle;
        [SerializeField] private RectTransform _avatarsGrid;
        [SerializeField] private TMP_Text _framesTitle;
        [SerializeField] private RectTransform _framesGrid;
        [SerializeField] private AppearanceCellView _cellTemplate;

        [Header("Достижения")]
        [SerializeField] private UiButtonView _achievementsTab;
        [SerializeField] private GameObject _achievementsPage;
        [SerializeField] private TMP_Text _achievementsSummary;
        [SerializeField] private RectTransform _achievementsList;
        [SerializeField] private AchievementRowView _achievementTemplate;

        private enum Page
        {
            Stats,
            Look,
            Achievements,
            Talents,
        }

        private IPlayerRecords _records;
        private IAchievements _achievements;
        private UiKit _kit;
        private readonly List<AchievementRowView> _achievementRows = new List<AchievementRowView>();
        private IPlayerLevel _level;
        private IDieMastery _mastery;
        private ContentDatabase _content;
        private IInventory _inventory;
        private ILoadout _loadout;
        private UiPalette _palette;
        private readonly List<StatRowView> _rows = new List<StatRowView>();
        private readonly List<AppearanceCellView> _cells = new List<AppearanceCellView>();
        private Page _page;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform list, StatRowView rowTemplate, UiButtonView back)
        {
            _title = title;
            _list = list;
            _rowTemplate = rowTemplate;
            _back = back;
        }

        public void EditorSetupLook(UiButtonView statsTab, UiButtonView lookTab, GameObject statsPage, GameObject lookPage,
            PlayerAvatarView preview, TMP_Text lookInfo, TMP_Text avatarsTitle, RectTransform avatarsGrid, TMP_Text framesTitle,
            RectTransform framesGrid, AppearanceCellView cellTemplate)
        {
            _statsTab = statsTab;
            _lookTab = lookTab;
            _statsPage = statsPage;
            _lookPage = lookPage;
            _preview = preview;
            _lookInfo = lookInfo;
            _avatarsTitle = avatarsTitle;
            _avatarsGrid = avatarsGrid;
            _framesTitle = framesTitle;
            _framesGrid = framesGrid;
            _cellTemplate = cellTemplate;
        }

        public void EditorSetupAchievements(UiButtonView tab, GameObject page, TMP_Text summary, RectTransform list,
            AchievementRowView template)
        {
            _achievementsTab = tab;
            _achievementsPage = page;
            _achievementsSummary = summary;
            _achievementsList = list;
            _achievementTemplate = template;
        }
#endif

        [Inject]
        public void Construct(IPlayerRecords records, IPlayerLevel level, IDieMastery mastery, ContentDatabase content, GameConfig config,
            IInventory inventory, ILoadout loadout, [InjectOptional] IAchievements achievements, [InjectOptional] UiKit kit)
        {
            _kit = kit;
            _achievements = achievements;
            _records = records;
            _level = level;
            _mastery = mastery;
            _content = content;
            _inventory = inventory;
            _loadout = loadout;
            _palette = config != null && config.Ui != null ? config.Ui.Palette : new UiPalette();
        }

        private void Awake()
        {
            // Строка может быть деталью-префабом или шаблоном внутри окна: выключаем только шаблон.
            if (_rowTemplate.gameObject.scene.IsValid())
                _rowTemplate.gameObject.SetActive(false);
            if (_cellTemplate != null && _cellTemplate.gameObject.scene.IsValid())
                _cellTemplate.gameObject.SetActive(false);
            if (_achievementTemplate != null && _achievementTemplate.gameObject.scene.IsValid())
                _achievementTemplate.gameObject.SetActive(false);
            _back.OnClick(RequestClose);
            if (_statsTab != null)
                _statsTab.OnClick(() => ShowPage(Page.Stats));
            if (_lookTab != null)
                _lookTab.OnClick(() => ShowPage(Page.Look));
            if (_achievementsTab != null)
                _achievementsTab.OnClick(() => ShowPage(Page.Achievements));
            AwakeTalents();
        }

        protected override void OnShowing()
        {
            _title.text = T("profile.title");
            _back.SetText(T("ui.back"));
            if (_statsTab != null)
                _statsTab.SetText(T("profile.stats"));
            if (_lookTab != null)
                _lookTab.SetText(T("profile.look"));
            if (_avatarsTitle != null)
                _avatarsTitle.text = T("profile.avatars");
            if (_framesTitle != null)
                _framesTitle.text = T("profile.frames");
            if (_achievementsTab != null)
                _achievementsTab.SetText(T("profile.achievements"));
            ShowingTalents();
            Build();
            ShowPage(_page);
        }

        // ---------- Вкладки ----------

        private void ShowPage(Page page)
        {
            var hasLook = _lookPage != null && _avatarsGrid != null && _cellTemplate != null;
            var hasAchievements = _achievementsPage != null && _achievementsList != null && _achievementTemplate != null &&
                                  _achievements != null;
            if ((page == Page.Look && !hasLook) || (page == Page.Achievements && !hasAchievements) || (page == Page.Talents && !HasTalents))
                page = Page.Stats;
            _page = page;

            if (_statsPage != null)
                _statsPage.SetActive(page == Page.Stats);
            if (_lookPage != null)
                _lookPage.SetActive(page == Page.Look);
            if (_achievementsPage != null)
                _achievementsPage.SetActive(page == Page.Achievements);
            PaintTab(_statsTab, true, page == Page.Stats);
            PaintTab(_lookTab, hasLook, page == Page.Look);
            PaintTab(_achievementsTab, hasAchievements, page == Page.Achievements);
            if (_talentsPage != null)
                _talentsPage.SetActive(page == Page.Talents);
            PaintTab(_talentsTab, HasTalents, page == Page.Talents);

            if (page == Page.Look)
                BuildLook();
            else if (page == Page.Achievements)
                BuildAchievements();
            else if (page == Page.Talents)
                BuildTalents();
        }

        private void PaintTab(UiButtonView tab, bool visible, bool selected)
        {
            if (tab == null)
                return;
            tab.SetVisible(visible);
            tab.SetColor(selected ? _palette.ButtonAccent : _palette.ButtonMuted);
        }

        // ---------- Достижения ----------

        /// <summary>Сначала начатые (по доле прогресса), потом не начатые, в конце открытые.</summary>
        private void BuildAchievements()
        {
            foreach (var row in _achievementRows)
                Destroy(row.gameObject);
            _achievementRows.Clear();

            _achievements.Check();
            // Открытое прямо сейчас (звёзды, коллекция): награда уже выдана, сообщаем здесь, а не после партии.
            foreach (var unlock in _achievements.TakeUnlocked())
            {
                if (_kit != null)
                    Toast.ShowAsync(_kit, transform, T("achievement.unlocked", RewardNames.AchievementTitle(unlock.Achievement, T)),
                        _palette.Gold, 1.2f, this.GetCancellationTokenOnDestroy()).Forget();
            }

            var all = new List<AchievementConfig>(_achievements.All);
            all.Sort((a, b) => SortKey(b).CompareTo(SortKey(a)));
            if (_achievementsSummary != null)
                _achievementsSummary.text = T("profile.achievementsSummary", _achievements.UnlockedCount, all.Count);

            foreach (var achievement in all)
            {
                var row = Instantiate(_achievementTemplate, _achievementsList);
                row.gameObject.SetActive(true);
                _achievementRows.Add(row);
                var unlocked = _achievements.IsUnlocked(achievement);
                var rewards = RewardNames.List(achievement.Rewards, T);
                row.Show(RewardNames.AchievementTitle(achievement, T), RewardNames.AchievementDescription(achievement, T),
                    _achievements.ProgressOf(achievement), achievement.Target,
                    string.IsNullOrEmpty(rewards) ? string.Empty : T("profile.achievementReward", rewards), unlocked,
                    _palette.RarityColor(achievement.Rarity), _palette.Locked, _palette.Text, _palette.TextMuted);
            }
        }

        private float SortKey(AchievementConfig achievement)
        {
            if (_achievements.IsUnlocked(achievement))
                return -1f;
            var fraction = achievement.Target > 0 ? (float)_achievements.ProgressOf(achievement) / achievement.Target : 0f;
            return Mathf.Clamp01(fraction) - achievement.Order * 1e-6f;
        }

        // ---------- Облик ----------

        private void BuildLook()
        {
            foreach (var cell in _cells)
                Destroy(cell.gameObject);
            _cells.Clear();

            AddCells(SlotIds.Avatar, _avatarsGrid);
            AddCells(SlotIds.Frame, _framesGrid);
            if (_preview != null)
                _preview.ShowEquipped();
            if (_lookInfo != null)
                _lookInfo.text = T("profile.lookHint");
        }

        private void AddCells(string slotId, RectTransform grid)
        {
            var slot = _content.Get<CosmeticSlotConfig>(slotId);
            if (slot == null || grid == null)
                return;

            var items = _content.All<CosmeticItemConfig>().FindAll(i => i.Slot == slot);
            items.Sort((a, b) => a.Rarity != b.Rarity ? a.Rarity.CompareTo(b.Rarity) : a.Order.CompareTo(b.Order));
            foreach (var item in items)
            {
                var cell = Instantiate(_cellTemplate, grid);
                cell.gameObject.SetActive(true);
                var captured = item;
                cell.Clicked += () => OnCellClicked(captured);
                _cells.Add(cell);
                PaintCell(cell, item);
            }
        }

        private void PaintCell(AppearanceCellView cell, CosmeticItemConfig item)
        {
            cell.Show(PlayerAvatarView.SpriteOf(item), _palette.RarityColor(item.Rarity), _inventory.IsOwned(item),
                _loadout.IsEquipped(item), _palette.Locked);
        }

        private void OnCellClicked(CosmeticItemConfig item)
        {
            var owned = _inventory.IsOwned(item);
            if (owned)
            {
                _loadout.Equip(item);
                BuildLook();
                if (_lookInfo != null)
                    _lookInfo.text = T("profile.equipped", Describe(item));
                return;
            }

            // Не открыт: примерка на большом аватаре и подсказка, где взять.
            if (_preview != null)
            {
                var avatar = item.Slot.Id == SlotIds.Avatar ? item : Equipped(SlotIds.Avatar);
                var frame = item.Slot.Id == SlotIds.Frame ? item : Equipped(SlotIds.Frame);
                _preview.ShowItems(avatar, frame);
            }

            if (_lookInfo != null)
            {
                var hint = HintOf(item);
                _lookInfo.text = string.IsNullOrEmpty(hint) ? Describe(item) : T("profile.lockedInfo", Describe(item), hint);
            }
        }

        private CosmeticItemConfig Equipped(string slotId)
        {
            var slot = _content.Get<CosmeticSlotConfig>(slotId);
            return slot != null ? _loadout.GetEquipped(slot) : null;
        }

        /// <summary>«Корона · легендарный».</summary>
        private string Describe(CosmeticItemConfig item)
        {
            return T(item.NameKey) + " · " + T("rarity." + item.Rarity.ToString().ToLowerInvariant());
        }

        private string HintOf(CosmeticItemConfig item)
        {
            if (item.Price == null || item.Price.Options == null)
                return null;

            foreach (var option in item.Price.Options)
            {
                if (option is ProgressPriceOption progress && !string.IsNullOrEmpty(progress.HintKey))
                    return T(progress.HintKey);
            }

            return null;
        }

        private void Build()
        {
            foreach (var row in _rows)
                Destroy(row.gameObject);
            _rows.Clear();

            var data = _records.Data;
            AddGeneral(data);
            AddModes(data);
            AddTurns(data);
            AddCombos(data);
            AddLuck(data);
            AddDice(data);
        }

        private void AddGeneral(RecordsSave data)
        {
            Header("stats.general");
            if (_level != null)
            {
                _level.GetProgress(_level.Xp, out var number, out var into, out var toNext);
                Row("stats.level", T("stats.levelValue", number, into, toNext));
            }

            Row("stats.matches", data.Matches.ToString());
            Row("stats.wins", WinsOf(data.Wins, data.Matches));
            Row("stats.bestStreak", data.BestStreak.ToString());
            Row("stats.currentStreak", data.CurrentStreak.ToString());
            var minutes = data.SecondsPlayed / 60;
            Row("stats.timePlayed", T("stats.timeValue", minutes / 60, minutes % 60));
        }

        private void AddModes(RecordsSave data)
        {
            Header("stats.modes");
            ModeRow(data, TableStateIds.Campaign, "menu.campaign");
            ModeRow(data, TableStateIds.Tower, "mode.tower");
            ModeRow(data, TableStateIds.EndlessRun, "mode.endlessRun");
            Row("stats.bosses", WinsOf(data.BossWins, data.BossMatches));
        }

        private void ModeRow(RecordsSave data, string mode, string nameKey)
        {
            var record = data.Modes.Find(m => m.Id == mode);
            Row(nameKey, record != null ? WinsOf(record.Wins, record.Matches) : WinsOf(0, 0));
        }

        private void AddTurns(RecordsSave data)
        {
            Header("stats.turns");
            Row("stats.rolls", data.Rolls.ToString());
            Row("stats.zonks", T("stats.countShare", data.Zonks, Percent(data.Zonks, data.Rolls)));
            Row("stats.bestTurn", data.BestTurn.ToString());
            Row("stats.avgTurn", data.Banks > 0 ? (data.PointsBanked / data.Banks).ToString() : "—");
            Row("stats.points", data.PointsBanked.ToString());
            Row("stats.hotDice", data.HotDice.ToString());
        }

        private void AddCombos(RecordsSave data)
        {
            Header("stats.combos");
            Row("stats.combo.straight", data.Combos[(int)RecordedCombo.Straight].ToString());
            Row("stats.combo.threePairs", data.Combos[(int)RecordedCombo.ThreePairs].ToString());
            Row("stats.combo.three", data.Combos[(int)RecordedCombo.ThreeOfAKind].ToString());
            Row("stats.combo.four", data.Combos[(int)RecordedCombo.FourOfAKind].ToString());
            Row("stats.combo.five", data.Combos[(int)RecordedCombo.FiveOfAKind].ToString());
            Row("stats.combo.six", data.Combos[(int)RecordedCombo.SixOfAKind].ToString());
        }

        /// <summary>
        /// Удача: Зонки на бросках только обычными костями против честной вероятности (LuckMath) и доля единиц
        /// и пятёрок на обычных костях против 1/3.
        /// </summary>
        private void AddLuck(RecordsSave data)
        {
            Header("stats.luck");

            var rolls = 0;
            var zonks = 0;
            var expected = 0.0;
            for (var dice = 1; dice <= 6; dice++)
            {
                rolls += data.FairRolls[dice];
                zonks += data.FairZonks[dice];
                expected += data.FairRolls[dice] * LuckMath.ZonkChance(dice);
            }

            if (rolls < MinLuckRolls || expected <= 0)
            {
                Row("stats.luckVerdict", T("stats.luckFew", MinLuckRolls - rolls), _palette.TextMuted);
            }
            else
            {
                var luck = Mathf.RoundToInt((float)((expected - zonks) / expected * 100.0));
                var color = luck > 0 ? _palette.Good : luck < 0 ? _palette.Bad : _palette.Text;
                var verdict = luck > 0 ? T("stats.luckGood", luck) : luck < 0 ? T("stats.luckBad", -luck) : T("stats.luckEven");
                Row("stats.luckVerdict", verdict, color);
                Row("stats.luckZonks", T("stats.luckZonksValue", zonks, Mathf.RoundToInt((float)expected)));
            }

            if (data.FairFaces > 0)
            {
                var share = (double)data.FairOnesFives / data.FairFaces * 100.0;
                Row("stats.onesFives", T("stats.onesFivesValue", share.ToString("0.0"),
                    (LuckMath.OnesAndFivesShare * 100.0).ToString("0.0")));
            }
        }

        private void AddDice(RecordsSave data)
        {
            if (data.Dice.Count == 0)
                return;

            Header("stats.dice");
            DieRecord favorite = null;
            foreach (var record in data.Dice)
            {
                if (favorite == null || record.Matches > favorite.Matches)
                    favorite = record;
            }

            var favoriteDie = favorite != null ? FindDie(favorite.Id) : null;
            if (favoriteDie != null)
                Row("stats.favoriteDie", T(favoriteDie.NameKey), _palette.Gold);

            foreach (var record in data.Dice)
            {
                var die = FindDie(record.Id);
                if (die == null)
                    continue;

                var value = WinsOf(record.Wins, record.Matches);
                var level = _mastery != null ? _mastery.GetLevel(die) : 0;
                var info = level > 0 ? _mastery.GetLevelInfo(level) : null;
                if (info != null)
                    value += " · " + T(info.NameKey);
                AddRow(T(die.NameKey), value, _palette.Text);
            }
        }

        private DieConfig FindDie(string id)
        {
            foreach (var die in _content.All<DieConfig>())
            {
                if (die.Id == id)
                    return die;
            }

            return null;
        }

        private string WinsOf(int wins, int matches)
        {
            return T("stats.winsOf", wins, matches, Percent(wins, matches));
        }

        private static string Percent(long part, long total)
        {
            return total > 0 ? Mathf.RoundToInt(part * 100f / total) + "%" : "0%";
        }

        private void Header(string key)
        {
            Spawn().SetHeader(T(key), _palette.Gold);
        }

        private void Row(string key, string value)
        {
            AddRow(T(key), value, _palette.Text);
        }

        private void Row(string key, string value, Color valueColor)
        {
            AddRow(T(key), value, valueColor);
        }

        private void AddRow(string label, string value, Color valueColor)
        {
            Spawn().SetRow(label, value, _palette.TextMuted, valueColor);
        }

        private StatRowView Spawn()
        {
            var row = Instantiate(_rowTemplate, _list);
            row.gameObject.SetActive(true);
            _rows.Add(row);
            return row;
        }
    }
}
