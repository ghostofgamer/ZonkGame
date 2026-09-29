using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>
    /// Общие настройки игры. Один ассет, подключён в ZonkInstaller. Здесь всё, что не является
    /// отдельным контентом: стартовые значения, пределы, тайминги.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Интерфейс")]
        public Zonk.UI.UiConfig Ui;

        [Header("Режимы")]
        public GameModeConfig HotSeatMode;
        public GameModeConfig CampaignMode;

        [Header("Валюты")]
        public CurrencyConfig Coins;
        public CurrencyConfig Energy;

        [Header("Кости")]
        public DieConfig StandardDie;

        [Tooltip("Сколько особых костей можно взять в набор из шести")]
        public int MaxSpecialDice = 2;

        [Header("Игра вдвоём")]
        public int HotSeatMinTarget = 1000;
        public int HotSeatMaxTarget = 10000;
        public int HotSeatTargetStep = 500;
        public int HotSeatDefaultTarget = 4000;

        [Tooltip("Скин второго игрока, если оба выбрали одинаковый")]
        public CosmeticItemConfig SecondPlayerFallbackSkin;

        public Color[] PlayerColors = { new Color(0.95f, 0.75f, 0.3f), new Color(0.4f, 0.75f, 0.95f) };

        [Header("Реакции")]
        [Tooltip("Ход от стольких очков считается крупным")]
        public int BigBankScore = 1000;

        [Tooltip("Отложенная за раз комбинация от стольких очков считается крупной: соперник может ударить по столу")]
        public int BigKeepScore = 1000;

        [Tooltip("Бросок при стольких очках хода и 1–2 костях считается рискованным")]
        public int RiskyRollScore = 500;

        [Header("Стол и бросок")]
        [Tooltip("Ребро кости в метрах сцены. Модель 0.3, меньше = кости мельче относительно стакана и лотка")]
        public float DieSize = 0.24f;

        [Tooltip("Стиль броска по умолчанию: тряска и высыпание")]
        public RollStyleConfig RollStyle;

        [Header("Анимация")]
        public float SettleDuration = 0.35f;
        public float KeepDuration = 0.3f;
    }
}
