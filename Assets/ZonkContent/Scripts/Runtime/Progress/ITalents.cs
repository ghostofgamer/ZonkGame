using System;
using System.Collections.Generic;
using Zonk.Configs;

namespace Zonk.Progress
{
    /// <summary>
    /// Что даёт талант. Значение — сумма по взятым рангам (TalentConfig): проценты — в процентах (15 = +15%),
    /// количества — штуками. 0 — таланта нет. Новый эффект — значение здесь и его чтение в месте действия.
    /// Таланты не дают силы в обычной партии (кампания, игра вдвоём): только выгода, удобство, облик и рогалики
    /// (забег, башня), где рост силы — часть жанра.
    /// </summary>
    public enum TalentEffect
    {
        // Купец: выгода.
        CoinsOnWinPercent,
        ChestWinsMinus,
        EnergyMax,
        EnergyRegenPercent,
        StakeOptionsExtra,
        StakeRefundPercent,
        DailyQuestsExtra,
        QuestRerollsExtra,
        FirstWinDoubleCoins,

        // Мастер: кости, опыт, рогалики.
        MasteryPercent,
        XpPercent,
        DicePresetsExtra,
        BestMoveHint,
        RunStartHearts,
        RunStartPerk,
        RunPerkOffersExtra,
        RunFreeRerolls,
        TowerStartHearts,
        TowerFreeRevives,

        // Коллекционер: облик и магазин.
        ShopCoinDiscountPercent,
        DecorSpotsExtra,
        WinCelebration,
        Title,
    }

    /// <summary>Таланты игрока: очки за уровни и трудные достижения, ранги в дереве (Купец, Мастер, Коллекционер).</summary>
    public interface ITalents
    {
        /// <summary>Сумма эффекта по взятым рангам; 0 — нет.</summary>
        float Value(TalentEffect effect);

        int TotalPoints { get; }
        int SpentPoints { get; }
        int FreePoints { get; }

        /// <summary>Все узлы по веткам, рядам и порядку.</summary>
        IReadOnlyList<TalentConfig> All { get; }

        int RankOf(TalentConfig talent);

        /// <summary>Сколько очков вложено в ветку.</summary>
        int PointsIn(TalentBranch branch);

        /// <summary>Требования узла выполнены (ранг можно взять при наличии очков).</summary>
        bool IsOpen(TalentConfig talent);

        bool CanRankUp(TalentConfig talent);
        bool TryRankUp(TalentConfig talent);

        /// <summary>Вернуть все очки (монеты за сброс списывает вызывающий: GameConfig.TalentResetCoins).</summary>
        void Reset();

        event Action Changed;
    }
}
