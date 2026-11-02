using System.Collections.Generic;
using Base.Core.Localization;
using TMPro;
using UnityEngine;
using Zonk.Configs;
using Zonk.Core.Match;
using Zonk.Progress;

namespace Zonk.UI.Views
{
    /// <summary>
    /// Настройки одного игрока в игре вдвоём: имя, кости, скин. Два таких блока в окне HotSeatSetupWindow.
    /// Игрок 1 — хозяин аккаунта: его кости по умолчанию — активный набор («Мои кости»), кнопкой источника выбирается
    /// любой набор или «свои». Смена одной кости набора переводит игрока на свои кости (копию набора с правкой):
    /// сам набор аккаунта не меняется. «Все обычные» — шесть обычных костей, чтобы проверить новое на простом.
    /// </summary>
    public sealed class PlayerSetupPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_InputField _name;
        [SerializeField] private TMP_Text _diceLabel;
        [SerializeField] private DiceLoadoutView _dice;
        [SerializeField] private UiButtonView _skin;

        [Tooltip("Откуда кости: набор аккаунта или свои (только у игрока 1). Пусто — кнопки нет: старый префаб, свои кости")]
        [SerializeField] private UiButtonView _source;

        [Tooltip("Все кости обычные. Пусто — кнопки нет: старый префаб")]
        [SerializeField] private UiButtonView _allStandard;

        private ILocalization _localization;
        private HotSeatPlayerSave _player;
        private HotSeatSettings _settings;
        private GameConfig _config;
        private ContentDatabase _content;
        private IInventory _inventory;
        private ILoadout _loadout;
        private List<DieConfig> _ownedDice;
        private List<CosmeticItemConfig> _skins;
        private CosmeticItemConfig _fallbackSkin;
        private int _index;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, TMP_InputField name, TMP_Text diceLabel, DiceLoadoutView dice, UiButtonView skin)
        {
            _title = title;
            _name = name;
            _diceLabel = diceLabel;
            _dice = dice;
            _skin = skin;
        }

        public void EditorSetupDiceSource(UiButtonView source, UiButtonView allStandard)
        {
            _source = source;
            _allStandard = allStandard;
        }

        public bool HasDiceSource => _source != null;
#endif

        public void Setup(ILocalization localization, int index, HotSeatPlayerSave player, HotSeatSettings settings,
            GameConfig config, ContentDatabase content, IInventory inventory, List<DieConfig> ownedDice,
            List<CosmeticItemConfig> ownedSkins, ILoadout loadout = null)
        {
            _localization = localization;
            _index = index;
            _player = player;
            _settings = settings;
            _config = config;
            _content = content;
            _inventory = inventory;
            _loadout = loadout;
            _ownedDice = ownedDice;
            _skins = ownedSkins;
            _fallbackSkin = config.SecondPlayerFallbackSkin;

            var color = config.PlayerColors != null && index < config.PlayerColors.Length ? config.PlayerColors[index] : Color.white;
            _title.text = Format("hotseat.player", index + 1);
            _title.color = color;
            _diceLabel.text = localization.Get("hotseat.dice");

            if (string.IsNullOrEmpty(player.Name))
                player.Name = Format("hotseat.player", index + 1);
            _name.text = player.Name;
            _name.onEndEdit.RemoveAllListeners();
            _name.onEndEdit.AddListener(value =>
            {
                player.Name = string.IsNullOrWhiteSpace(value) ? Format("hotseat.player", index + 1) : value.Trim();
                settings.Save();
            });

            // Без кнопки источника (старый префаб) или без наборов — только свои кости, как раньше.
            if (!UsesPresets)
                player.Preset = HotSeatPlayerSave.OwnDice;

            if (_source != null)
            {
                _source.SetVisible(UsesPresets);
                _source.OnClick(NextSource);
            }

            if (_allStandard != null)
            {
                _allStandard.SetText(localization.Get("dice.allStandard"));
                _allStandard.OnClick(AllStandard);
            }

            ShowDice();
            _skin.OnClick(CycleSkin);
            RefreshSkin();
        }

        /// <summary>Наборы аккаунта доступны игроку 1, если в префабе есть кнопка источника.</summary>
        private bool UsesPresets => _index == 0 && _source != null && _loadout != null;

        private bool OnPreset => UsesPresets && _player.Preset != HotSeatPlayerSave.OwnDice;

        private void ShowDice()
        {
            IReadOnlyList<DieConfig> dice;
            if (OnPreset)
                dice = _player.Preset == HotSeatPlayerSave.ActivePreset ? _loadout.GetDice() : _loadout.GetPresetDice(_player.Preset);
            else
                dice = Loadout.ResolveDice(_player.Dice, _content, _config, _inventory);

            _dice.Setup(_localization, _ownedDice, dice, _config.MaxSpecialDice, OnDieChanged);
            RefreshSource();
        }

        /// <summary>Кость сменилась. На наборе — игрок переходит на свои кости: копия набора с этой правкой.</summary>
        private void OnDieChanged(int slot, DieConfig die)
        {
            if (OnPreset)
            {
                _player.Dice.Clear();
                for (var i = 0; i < _dice.Dice.Count; i++)
                    _player.Dice.Add(_dice.Dice[i] != null ? _dice.Dice[i].Id : string.Empty);
                _player.Preset = HotSeatPlayerSave.OwnDice;
                RefreshSource();
            }
            else
            {
                while (_player.Dice.Count <= slot)
                    _player.Dice.Add(_config.StandardDie != null ? _config.StandardDie.Id : string.Empty);
                _player.Dice[slot] = die.Id;
            }

            _settings.Save();
        }

        /// <summary>Активный набор → набор 1 → … → свои → снова активный.</summary>
        private void NextSource()
        {
            var count = _loadout.PresetCount;
            if (_player.Preset == HotSeatPlayerSave.ActivePreset)
                _player.Preset = 0;
            else if (_player.Preset == HotSeatPlayerSave.OwnDice)
                _player.Preset = HotSeatPlayerSave.ActivePreset;
            else if (_player.Preset + 1 < count)
                _player.Preset++;
            else
                _player.Preset = HotSeatPlayerSave.OwnDice;

            _settings.Save();
            ShowDice();
        }

        private void AllStandard()
        {
            var standard = _config.StandardDie != null ? _config.StandardDie.Id : string.Empty;
            _player.Dice.Clear();
            for (var i = 0; i < ZonkMatch.DiceCount; i++)
                _player.Dice.Add(standard);
            _player.Preset = HotSeatPlayerSave.OwnDice;
            _settings.Save();
            ShowDice();
        }

        private void RefreshSource()
        {
            if (_source == null || !UsesPresets)
                return;

            switch (_player.Preset)
            {
                case HotSeatPlayerSave.ActivePreset:
                    _source.SetText(Format("hotseat.sourceActive", _loadout.ActivePreset + 1));
                    break;
                case HotSeatPlayerSave.OwnDice:
                    _source.SetText(_localization.Get("hotseat.sourceOwn"));
                    break;
                default:
                    _source.SetText(Format("hotseat.sourcePreset", _player.Preset + 1));
                    break;
            }

            _source.SetColor(OnPreset ? UiColors.ButtonAccent : UiColors.Button);
        }

        private void CycleSkin()
        {
            if (_skins.Count == 0)
                return;

            var current = _skins.FindIndex(s => s.Id == _player.SkinId);
            _player.SkinId = _skins[(current + 1) % _skins.Count].Id;
            _settings.Save();
            RefreshSkin();
        }

        private void RefreshSkin()
        {
            var skin = _skins.Find(s => s.Id == _player.SkinId);
            if (skin == null && _skins.Count > 0)
            {
                // У второго игрока по умолчанию запасной скин, чтобы кости игроков различались.
                skin = _index == 1 && _fallbackSkin != null && _skins.Contains(_fallbackSkin) ? _fallbackSkin : _skins[0];
                _player.SkinId = skin.Id;
            }

            _skin.SetText(Format("hotseat.skin", skin != null ? _localization.Get(skin.NameKey) : "-"));
        }

        private string Format(string key, params object[] args)
        {
            return UiFormat.Format(_localization, key, args);
        }
    }
}
