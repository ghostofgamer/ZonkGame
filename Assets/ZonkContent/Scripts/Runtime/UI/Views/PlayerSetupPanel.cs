using System.Collections.Generic;
using Base.Core.Localization;
using TMPro;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.UI.Views
{
    /// <summary>Настройки одного игрока в игре вдвоём: имя, кости, скин. Два таких блока в окне HotSeatSetupWindow.</summary>
    public sealed class PlayerSetupPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_InputField _name;
        [SerializeField] private TMP_Text _diceLabel;
        [SerializeField] private DiceLoadoutView _dice;
        [SerializeField] private UiButtonView _skin;

        private ILocalization _localization;
        private HotSeatPlayerSave _player;
        private HotSeatSettings _settings;
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
#endif

        public void Setup(ILocalization localization, int index, HotSeatPlayerSave player, HotSeatSettings settings,
            GameConfig config, ContentDatabase content, IInventory inventory, List<DieConfig> ownedDice,
            List<CosmeticItemConfig> ownedSkins)
        {
            _localization = localization;
            _index = index;
            _player = player;
            _settings = settings;
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

            var dice = Loadout.ResolveDice(player.Dice, content, config, inventory);
            _dice.Setup(localization, ownedDice, dice, config.MaxSpecialDice, (slot, die) =>
            {
                player.Dice[slot] = die.Id;
                settings.Save();
            });

            _skin.OnClick(CycleSkin);
            RefreshSkin();
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
            try
            {
                return string.Format(_localization.Get(key), args);
            }
            catch (System.FormatException)
            {
                return _localization.Get(key);
            }
        }
    }
}
