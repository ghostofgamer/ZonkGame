using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Modifiers;

namespace Zonk.Configs
{
    /// <summary>
    /// Башня — конечное испытание: этажи по порядку, у каждого свой соперник, цель и правила. Попытка начинается
    /// с последнего рубежа (каждые CheckpointEvery этажей прогресс сохраняется навсегда), на попытку HeartsPerAttempt
    /// сердец. Новые этажи добавляются строками в Floors — игроки продолжат с рубежа, на котором остановились.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Tower", fileName = "Tower")]
    public sealed class TowerConfig : ScriptableObject
    {
        [Tooltip("Энергия за попытку (этажи внутри попытки бесплатны)")]
        public int EnergyCost = 1;

        [Tooltip("Сердец на попытку: проигрыш забирает одно, этаж переигрывается")]
        public int HeartsPerAttempt = 3;

        [Tooltip("Рубеж каждые столько этажей: дальше попытки начинаются с него")]
        public int CheckpointEvery = 5;

        [Tooltip("Один раз за попытку продолжить за рекламу (+1 сердце), когда сердца кончились")]
        public bool ReviveForAd = true;

        [Tooltip("Монет за этаж, пройденный повторно (первое прохождение — награда этажа)")]
        public int RepeatCoins = 5;

        [Tooltip("Этажи снизу вверх")]
        public List<TowerFloor> Floors = new List<TowerFloor>();
    }

    [Serializable]
    public sealed class TowerFloor
    {
        [Tooltip("Соперник этажа: его кости, ИИ и правила (у боссов — их правила)")]
        public OpponentConfig Opponent;

        [Tooltip("Цель партии на этаже")]
        public int Target = 3000;

        [Tooltip("Правила этажа сверх правил соперника (действуют на обоих)")]
        [SerializeReference, SubclassSelector]
        public List<MatchModifier> Rules = new List<MatchModifier>();

        [Tooltip("Награда за первое прохождение этажа")]
        [SerializeReference, SubclassSelector]
        public List<Reward> FirstClearRewards = new List<Reward>();
    }
}
