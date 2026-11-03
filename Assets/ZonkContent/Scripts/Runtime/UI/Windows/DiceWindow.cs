using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Progress;
using Zonk.Table;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// «Мои кости» прямо на столе, как магазин: камера на лотке, в нём шесть костей активного набора в настоящем виде
    /// (модель особой кости, накладки, мастерство). Нажатие кости на столе выбирает место, карточка снизу кладёт на него
    /// кость. Вкладки — наборы (ILoadout: 3 и больше с талантом), смена сразу сохраняется на аккаунт.
    /// Активный набор играет в кампании, башне и забеге; в игре вдвоём — у игрока 1 по умолчанию.
    /// </summary>
    public sealed class DiceWindow : UiWindow
    {
        /// <summary>Ракурс камеры на лоток с костями (тот же, что у вкладки особых костей магазина).</summary>
        public const string CameraShot = "shop_dice";

        [SerializeField] private TMP_Text _title;
        [SerializeField] private RectTransform _presets;
        [SerializeField] private UiButtonView _presetTemplate;
        [SerializeField] private UiButtonView _allStandard;
        [SerializeField] private UiButtonView _back;

        protected override UiButtonView BackButton => _back;

        [SerializeField] private RectTransform _items;

        [Tooltip("Префаб карточки (Prefabs/UI/Parts/ShopCard): кость, её мастерство, «в наборе»")]
        [SerializeField] private ShopCardView _cardPrefab;

        [SerializeField] private TMP_Text _slotName;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _specials;
        [SerializeField] private TMP_Text _hint;

        private readonly List<UiButtonView> _presetButtons = new List<UiButtonView>();
        private readonly Dictionary<DieConfig, ShopCardView> _cards = new Dictionary<DieConfig, ShopCardView>();
        private readonly List<int> _masteryLevels = new List<int>();
        private ILoadout _loadout;
        private OwnedContent _owned;
        private TableView _table;
        private IDieMastery _mastery;
        private GameConfig _config;
        private UiKit _kit;
        private List<DieConfig> _available;
        private IReadOnlyList<DieConfig> _dice;
        private int _slot;
        private Action<DieView> _dieClicked;

        [Inject]
        public void Construct(ILoadout loadout, OwnedContent owned, TableView table, IDieMastery mastery, GameConfig config, UiKit kit)
        {
            _loadout = loadout;
            _owned = owned;
            _table = table;
            _mastery = mastery;
            _config = config;
            _kit = kit;
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, RectTransform presets, UiButtonView presetTemplate, UiButtonView allStandard,
            UiButtonView back, RectTransform items, ShopCardView cardPrefab, TMP_Text slotName, TMP_Text description,
            TMP_Text specials, TMP_Text hint)
        {
            _title = title;
            _presets = presets;
            _presetTemplate = presetTemplate;
            _allStandard = allStandard;
            _back = back;
            _items = items;
            _cardPrefab = cardPrefab;
            _slotName = slotName;
            _description = description;
            _specials = specials;
            _hint = hint;
        }
#endif

        /// <summary>Открыть окно поверх стола и дождаться закрытия (главное меню, кампания).</summary>
        public static async UniTask EditAsync(IUiService ui, CancellationToken ct)
        {
            var window = await ui.OpenAsync<DiceWindow>(ct);
            try
            {
                await window.RunAsync(ct);
            }
            finally
            {
                await ui.CloseAsync(window, CancellationToken.None);
            }
        }

        private void Awake()
        {
            _presetTemplate.gameObject.SetActive(false);
            _back.OnClick(RequestClose);
            _allStandard.OnClick(AllStandard);
            _dieClicked = OnDieClicked;
        }

        protected override void OnShowing()
        {
            _title.text = T("dice.title");
            _back.SetText(T("ui.back"));
            _allStandard.SetText(T("dice.allStandard"));
            if (_hint != null)
                _hint.text = T("dice.hint");

            foreach (var button in _presetButtons)
                Destroy(button.gameObject);
            _presetButtons.Clear();
            for (var i = 0; i < _loadout.PresetCount; i++)
            {
                var index = i;
                var button = _presetTemplate.Spawn(_presets);
                button.SetText(T("presets.slot", i + 1));
                button.OnClick(() => SelectPreset(index));
                _presetButtons.Add(button);
            }

            _allStandard.transform.SetAsLastSibling();
            _back.transform.SetAsLastSibling();
            _available = _owned.Dice();
            Reload();
        }

        public async UniTask RunAsync(CancellationToken ct)
        {
            _table.Camera.MoveToAsync(CameraShot, 0.6f, ct).Forget();
            var view = _table.Dice;
            for (var i = 0; i < view.Dice.Count; i++)
                view[i].Clicked += _dieClicked;

            try
            {
                await WaitCloseRequestAsync(ct);
            }
            finally
            {
                for (var i = 0; i < view.Dice.Count; i++)
                    view[i].Clicked -= _dieClicked;

                // Отмена приходит только при выгрузке стола: сцена уничтожается, убирать нечего.
                if (!ct.IsCancellationRequested)
                {
                    HideTable();
                    _table.Camera.MoveToAsync(CameraShots.Menu, 0.6f, ct).Forget();
                }
            }
        }

        private void SelectPreset(int index)
        {
            _loadout.SelectPreset(index);
            _table.Sound.Play(Sfx.Click);
            Reload();
        }

        private void OnDieClicked(DieView die)
        {
            _slot = Mathf.Clamp(die.Slot, 0, _dice.Count - 1);
            _table.Sound.Play(Sfx.Click);
            Refresh();
        }

        /// <summary>
        /// Положить кость на выбранное место. Особая кость уже на другом месте — места меняются (особая только одна);
        /// сверх лимита особых — подсказка.
        /// </summary>
        private void Put(DieConfig die)
        {
            var current = _dice[_slot];
            if (die == current)
                return;

            if (die.IsSpecial)
            {
                var elsewhere = IndexOf(die);
                if (elsewhere >= 0)
                {
                    _loadout.SetDie(elsewhere, current);
                }
                else if (SpecialCount(_slot) >= _config.MaxSpecialDice)
                {
                    ShowToast(T("dice.limit", _config.MaxSpecialDice), UiColors.Bad);
                    return;
                }
            }

            _loadout.SetDie(_slot, die);
            _table.Sound.Play(Sfx.Click);
            Reload();
        }

        private void AllStandard()
        {
            if (_config.StandardDie == null)
                return;

            for (var i = 0; i < _dice.Count; i++)
                _loadout.SetDie(i, _config.StandardDie);
            _table.Sound.Play(Sfx.Click);
            Reload();
        }

        /// <summary>Кости из сохранения заново (после смены набора или кости) и всё окно по ним.</summary>
        private void Reload()
        {
            _dice = _loadout.GetDice();
            _slot = Mathf.Clamp(_slot, 0, _dice.Count - 1);
            BuildCards();
            ShowTable();
            Refresh();
        }

        private void BuildCards()
        {
            _cards.Clear();
            foreach (Transform child in _items)
                Destroy(child.gameObject);

            foreach (var die in _available)
            {
                var captured = die;
                var card = Instantiate(_cardPrefab, _items);
                card.gameObject.SetActive(true);
                var inSet = IndexOf(die) >= 0;
                card.Setup(T(die.NameKey), die.Icon, null, inSet ? T("dice.inSet") : MasteryName(die),
                    inSet ? UiColors.ButtonAccent : UiColors.Button, () => Put(captured));
                _cards[die] = card;
            }
        }

        /// <summary>Выбранное место: подсветка кости на столе и карточки, описание кости, счётчик особых.</summary>
        private void Refresh()
        {
            var die = _dice[_slot];
            foreach (var pair in _cards)
                pair.Value.SetSelected(pair.Key == die);

            var view = _table.Dice;
            for (var i = 0; i < view.Dice.Count; i++)
                view[i].SetSelected(i == _slot);

            for (var i = 0; i < _presetButtons.Count; i++)
                _presetButtons[i].SetColor(i == _loadout.ActivePreset ? UiColors.ButtonAccent : UiColors.ButtonMuted);

            _slotName.text = T("dice.slot", _slot + 1, die != null ? T(die.NameKey) : "-");
            var description = die != null && !string.IsNullOrEmpty(die.DescriptionKey) ? T(die.DescriptionKey) : T("dice.standardDesc");
            if (die != null && die.IsSpecial)
                description += "\n" + MasteryText(die);
            _description.text = description;
            _specials.text = T("dice.specials", SpecialCount(-1), _config.MaxSpecialDice);
        }

        /// <summary>Шесть костей набора в лотке рядом, гранями 1..6 (номер места), в своём виде.</summary>
        private void ShowTable()
        {
            var view = _table.Dice;
            _masteryLevels.Clear();
            for (var i = 0; i < _dice.Count; i++)
                _masteryLevels.Add(_dice[i] != null && _dice[i].IsSpecial ? _mastery.GetLevel(_dice[i]) : 0);
            view.SetLoadout(_dice, _masteryLevels, _config.MasteryLevels);

            var yaw = _table.Tray.transform.eulerAngles.y;
            for (var i = 0; i < view.Dice.Count; i++)
            {
                var die = view[i];
                die.SetFace(i + 1, Quaternion.identity);
                die.transform.SetPositionAndRotation(_table.Tray.RowPosition(i, view.Dice.Count), die.RootRotationForFaceUp(yaw));
                die.Interactable = true;
                die.SetVisible(true);
            }
        }

        private void HideTable()
        {
            var view = _table.Dice;
            for (var i = 0; i < view.Dice.Count; i++)
            {
                view[i].Interactable = false;
                view[i].SetSelected(false);
            }

            view.SetVisible(false);
        }

        private int IndexOf(DieConfig die)
        {
            for (var i = 0; i < _dice.Count; i++)
            {
                if (_dice[i] == die)
                    return i;
            }

            return -1;
        }

        /// <summary>Сколько особых костей в наборе, не считая места skip (-1 — все).</summary>
        private int SpecialCount(int skip)
        {
            var count = 0;
            for (var i = 0; i < _dice.Count; i++)
            {
                if (i != skip && _dice[i] != null && _dice[i].IsSpecial)
                    count++;
            }

            return count;
        }

        private string MasteryName(DieConfig die)
        {
            if (!die.IsSpecial)
                return string.Empty;

            var info = _mastery.GetLevelInfo(_mastery.GetLevel(die));
            return info != null ? T(info.NameKey) : string.Empty;
        }

        /// <summary>Мастерство кости: уровень и очки до следующего уровня.</summary>
        private string MasteryText(DieConfig die)
        {
            var level = _mastery.GetLevel(die);
            var points = _mastery.GetPoints(die);
            var current = _mastery.GetLevelInfo(level);
            var next = _mastery.GetLevelInfo(level + 1);
            var name = current != null ? T(current.NameKey) : T("mastery.none");
            return next != null ? T("mastery.info", name, points, next.Points) : T("mastery.max", name, points);
        }

        private void ShowToast(string text, Color color)
        {
            Toast.ShowAsync(_kit, (RectTransform)transform, text, color, 0.9f, this.GetCancellationTokenOnDestroy()).Forget();
        }
    }
}
