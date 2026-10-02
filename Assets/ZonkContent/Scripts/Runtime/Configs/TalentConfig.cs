using UnityEngine;
using Zonk.Progress;

namespace Zonk.Configs
{
    /// <summary>Ветка дерева талантов.</summary>
    public enum TalentBranch
    {
        /// <summary>Купец: выгода и экономика.</summary>
        Merchant,

        /// <summary>Мастер: кости, опыт, рогалики (забег, башня).</summary>
        Master,

        /// <summary>Коллекционер: облик стола и магазин.</summary>
        Collector,
    }

    /// <summary>
    /// Узел дерева талантов: ветка и ряд, требования (предыдущий узел взят хотя бы на ранг, очков вложено в ветку),
    /// ранги и эффект за ранг (ITalents.Value — сумма по рангам). Описание — DescriptionKey, {0} — значение за ранг,
    /// {1} — сумма на максимуме. Новый узел — ассет; новый эффект — значение TalentEffect и его чтение в месте действия.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Talent", fileName = "Talent")]
    public sealed class TalentConfig : ContentConfig
    {
        public TalentBranch Branch;

        [Tooltip("Ряд в ветке сверху вниз (0 — первый)")]
        [Min(0)] public int Row;

        [Tooltip("Порядок внутри ряда (слева направо)")]
        public int Order;

        [Tooltip("Нужен хотя бы ранг этого узла (пусто — без требования)")]
        public TalentConfig Requires;

        [Tooltip("Нужно вложить в эту ветку не меньше очков")]
        [Min(0)] public int BranchPointsRequired;

        [Min(1)] public int MaxRank = 1;

        [Tooltip("Очков за один ранг")]
        [Min(1)] public int CostPerRank = 1;

        public TalentEffect Effect;

        [Tooltip("Значение эффекта за ранг: проценты — в процентах (5 = +5%), количества — штуками")]
        public float ValuePerRank = 1f;

        [Tooltip("Вершина ветки: крупнее в окне")]
        public bool Capstone;

        public string DescriptionKey;

        public Sprite Icon;
    }
}
