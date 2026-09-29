using TMPro;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.UI
{
    /// <summary>
    /// Игра вдвоём на одном экране: цель от 1000 до 10000, кто ходит первым, особые кости,
    /// у каждого игрока имя, свои кости и скин. Всё запоминается до следующего раза.
    /// </summary>
    public sealed class HotSeatSetupScreen : UiScreen
    {
        private readonly Choice<bool> _start = new Choice<bool>();

        public HotSeatSetupScreen(UiKit kit, RectTransform parent, HotSeatSettings settings, GameConfig config,
            ContentDatabase content, IInventory inventory, List<DieConfig> ownedDice, List<CosmeticItemConfig> ownedSkins)
            : base(kit, parent)
        {
            var data = settings.Data;

            kit.Blocker(Root, 0.35f);
            var panel = kit.Panel("HotSeat", Root, UiColors.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1500, 900));
            var column = kit.Column(panel.transform, 14, 28);
            UiKit.Stretch((RectTransform)column.transform);

            UiKit.Size(kit.Label(column.transform, kit.T("hotseat.title"), 44, TextAnchor.MiddleCenter, UiColors.Gold), -1, 70);

            // Цель партии.
            var targetRow = kit.Row(column.transform, 16);
            UiKit.Size(targetRow, -1, 72);
            var targetLabel = kit.Label(targetRow.transform, string.Empty, 32);
            kit.Button(targetRow.transform, "−", () => ChangeTarget(-1), UiColors.ButtonMuted, 40);
            kit.Button(targetRow.transform, "+", () => ChangeTarget(1), UiColors.ButtonMuted, 40);

            void ChangeTarget(int direction)
            {
                data.Target = settings.ClampTarget(data.Target + direction * config.HotSeatTargetStep);
                settings.Save();
                targetLabel.text = kit.T("hotseat.target", data.Target);
            }

            ChangeTarget(0);

            // Кто первый и особые кости.
            var optionsRow = kit.Row(column.transform, 16);
            UiKit.Size(optionsRow, -1, 72);
            UiButton firstButton = null, specialButton = null;
            firstButton = kit.Button(optionsRow.transform, string.Empty, () =>
            {
                data.FirstPlayer = (FirstPlayerMode)(((int)data.FirstPlayer + 1) % 3);
                settings.Save();
                RefreshOptions();
            }, UiColors.ButtonMuted, 26);
            specialButton = kit.Button(optionsRow.transform, string.Empty, () =>
            {
                data.SpecialDice = !data.SpecialDice;
                settings.Save();
                RefreshOptions();
            }, UiColors.ButtonMuted, 26);

            void RefreshOptions()
            {
                firstButton.SetText(kit.T("hotseat.first." + data.FirstPlayer.ToString().ToLowerInvariant()));
                specialButton.SetText(kit.T(data.SpecialDice ? "hotseat.specialOn" : "hotseat.specialOff"));
            }

            RefreshOptions();

            // Игроки.
            var playersRow = kit.Row(column.transform, 24);
            UiKit.Size(playersRow, -1, 420);
            for (var p = 0; p < HotSeatSettings.PlayerCount; p++)
                BuildPlayer(kit, playersRow.transform, p, data.Players[p], settings, config, content, inventory, ownedDice, ownedSkins);

            var buttons = kit.Row(column.transform, 24);
            UiKit.Size(buttons, -1, 84);
            kit.Button(buttons.transform, kit.T("ui.back"), () => _start.Set(false), UiColors.ButtonMuted);
            kit.Button(buttons.transform, kit.T("hotseat.start"), () => _start.Set(true), UiColors.ButtonAccent, 34);
        }

        private static void BuildPlayer(UiKit kit, Transform parent, int index, HotSeatPlayerSave player,
            HotSeatSettings settings, GameConfig config, ContentDatabase content, IInventory inventory, List<DieConfig> ownedDice,
            List<CosmeticItemConfig> ownedSkins)
        {
            var color = config.PlayerColors != null && index < config.PlayerColors.Length ? config.PlayerColors[index] : Color.white;
            var panel = kit.Panel("Player" + index, parent, UiColors.PanelLight);
            var column = kit.Column(panel.transform, 12, 20);
            UiKit.Stretch((RectTransform)column.transform);

            UiKit.Size(kit.Label(column.transform, kit.T("hotseat.player", index + 1), 32, TextAnchor.MiddleLeft, color), -1, 48);

            if (string.IsNullOrEmpty(player.Name))
                player.Name = kit.T("hotseat.player", index + 1);
            var input = kit.Input(column.transform, player.Name);
            input.onEndEdit.AddListener(value =>
            {
                player.Name = string.IsNullOrWhiteSpace(value) ? kit.T("hotseat.player", index + 1) : value.Trim();
                settings.Save();
            });

            UiKit.Size(kit.Label(column.transform, kit.T("hotseat.dice"), 24, TextAnchor.MiddleLeft, UiColors.TextMuted), -1, 36);
            var dice = Loadout.ResolveDice(player.Dice, content, config, inventory);
            new DiceLoadoutRow(kit, column.transform, ownedDice, dice, config.MaxSpecialDice, (slot, die) =>
            {
                player.Dice[slot] = die.Id;
                settings.Save();
            });

            UiButton skinButton = null;
            skinButton = kit.Button(column.transform, string.Empty, () =>
            {
                if (ownedSkins.Count == 0)
                    return;
                var current = ownedSkins.FindIndex(s => s.Id == player.SkinId);
                player.SkinId = ownedSkins[(current + 1) % ownedSkins.Count].Id;
                settings.Save();
                RefreshSkin();
            }, UiColors.ButtonMuted, 24);

            void RefreshSkin()
            {
                var skin = ownedSkins.Find(s => s.Id == player.SkinId);
                if (skin == null && ownedSkins.Count > 0)
                {
                    skin = ownedSkins[index == 1 && config.SecondPlayerFallbackSkin != null && ownedSkins.Contains(config.SecondPlayerFallbackSkin)
                        ? ownedSkins.IndexOf(config.SecondPlayerFallbackSkin)
                        : 0];
                    player.SkinId = skin.Id;
                }

                skinButton.SetText(kit.T("hotseat.skin", skin != null ? kit.T(skin.NameKey) : "-"));
            }

            RefreshSkin();
        }

        public UniTask<bool> RunAsync(CancellationToken ct)
        {
            return _start.WaitAsync(ct);
        }
    }
}
