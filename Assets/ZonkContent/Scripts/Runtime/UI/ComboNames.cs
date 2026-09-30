using Zonk.Core.Rules;

namespace Zonk.UI
{
    /// <summary>
    /// Ключ текста для названия отложенной комбинации во всплывающей надписи: «Стрит 1–6», «Три шестёрки»,
    /// «Пять одинаковых!». Одиночные единицы и пятёрки названия не получают. Из нескольких комбинаций
    /// берётся самая дорогая. Неизвестная категория (новое правило) — без названия.
    /// </summary>
    public static class ComboNames
    {
        private static readonly string[] Triples =
        {
            null, "combo.threeOnes", "combo.threeTwos", "combo.threeThrees", "combo.threeFours", "combo.threeFives",
            "combo.threeSixes",
        };

        public static string KeyFor(ScoreResult result)
        {
            if (result == null || result.Combos == null)
                return null;

            string best = null;
            var bestScore = -1;
            var combos = result.Combos;
            for (var i = 0; i < combos.Count; i++)
            {
                var combo = combos[i];
                var key = KeyFor(combo);
                if (key != null && combo.Score > bestScore)
                {
                    best = key;
                    bestScore = combo.Score;
                }
            }

            return best;
        }

        private static string KeyFor(ScoringCombo combo)
        {
            switch (combo.Category)
            {
                case ComboCategory.Straight:
                    if (combo.Used[1] > 0 && combo.Used[6] > 0)
                        return "combo.straight";
                    return combo.Used[1] > 0 ? "combo.straightLow" : "combo.straightHigh";
                case ComboCategory.ThreePairs:
                    return "combo.threePairs";
                case ComboCategory.OfAKind:
                    var count = combo.DiceCount;
                    if (count >= 6)
                        return "feel.sixKind";
                    if (count == 5)
                        return "feel.fiveKind";
                    if (count == 4)
                        return "feel.fourKind";
                    for (var face = 1; face <= 6; face++)
                    {
                        if (combo.Used[face] > 0)
                            return Triples[face];
                    }

                    return null;
                default:
                    return null;
            }
        }
    }
}
