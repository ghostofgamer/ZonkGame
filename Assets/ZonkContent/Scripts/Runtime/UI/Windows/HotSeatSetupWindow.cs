using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;
using Zonk.UI.Views;

namespace Zonk.UI.Windows
{
    /// <summary>
    /// Игра вдвоём на одном экране: цель от 1000 до 10000, кто ходит первым, особые кости,
    /// у каждого игрока имя, свои кости и скин (PlayerSetupPanel). Всё запоминается до следующего раза.
    /// </summary>
    public sealed class HotSeatSetupWindow : UiWindow
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _target;
        [SerializeField] private UiButtonView _minus;
        [SerializeField] private UiButtonView _plus;
        [SerializeField] private UiButtonView _firstPlayer;
        [SerializeField] private UiButtonView _specialDice;
        [SerializeField] private PlayerSetupPanel[] _players = new PlayerSetupPanel[2];
        [SerializeField] private UiButtonView _back;
        [SerializeField] private UiButtonView _start;

        private readonly Choice<bool> _choice = new Choice<bool>();
        private HotSeatSettings _settings;
        private GameConfig _config;

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text title, TMP_Text target, UiButtonView minus, UiButtonView plus, UiButtonView firstPlayer,
            UiButtonView specialDice, PlayerSetupPanel[] players, UiButtonView back, UiButtonView start)
        {
            _title = title;
            _target = target;
            _minus = minus;
            _plus = plus;
            _firstPlayer = firstPlayer;
            _specialDice = specialDice;
            _players = players;
            _back = back;
            _start = start;
        }
#endif

        private void Awake()
        {
            _minus.OnClick(() => ChangeTarget(-1));
            _plus.OnClick(() => ChangeTarget(1));
            _firstPlayer.OnClick(() =>
            {
                var data = _settings.Data;
                data.FirstPlayer = (FirstPlayerMode)(((int)data.FirstPlayer + 1) % 3);
                _settings.Save();
                RefreshOptions();
            });
            _specialDice.OnClick(() =>
            {
                var data = _settings.Data;
                data.SpecialDice = !data.SpecialDice;
                _settings.Save();
                RefreshOptions();
            });
            _back.OnClick(() => _choice.Set(false));
            _start.OnClick(() => _choice.Set(true));
        }

        public void Setup(HotSeatSettings settings, GameConfig config, ContentDatabase content, IInventory inventory,
            List<DieConfig> ownedDice, List<CosmeticItemConfig> ownedSkins)
        {
            _settings = settings;
            _config = config;
            var data = settings.Data;

            _title.text = T("hotseat.title");
            _back.SetText(T("ui.back"));
            _start.SetText(T("hotseat.start"));
            _minus.SetText("−");
            _plus.SetText("+");

            for (var i = 0; i < _players.Length && i < data.Players.Count; i++)
                _players[i].Setup(Localization, i, data.Players[i], settings, config, content, inventory, ownedDice, ownedSkins);

            ChangeTarget(0);
            RefreshOptions();
        }

        public UniTask<bool> WaitStartAsync(CancellationToken ct)
        {
            return _choice.WaitAsync(ct);
        }

        private void ChangeTarget(int direction)
        {
            var data = _settings.Data;
            data.Target = _settings.ClampTarget(data.Target + direction * _config.HotSeatTargetStep);
            _settings.Save();
            _target.text = T("hotseat.target", data.Target);
        }

        private void RefreshOptions()
        {
            var data = _settings.Data;
            _firstPlayer.SetText(T("hotseat.first." + data.FirstPlayer.ToString().ToLowerInvariant()));
            _specialDice.SetText(T(data.SpecialDice ? "hotseat.specialOn" : "hotseat.specialOff"));
        }
    }
}
