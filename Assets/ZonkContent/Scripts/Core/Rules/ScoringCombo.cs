namespace Zonk.Core.Rules
{
    /// <summary>Категории комбинаций: по ним модификаторы боссов меняют очки.</summary>
    public static class ComboCategory
    {
        public const string Single = "single";
        public const string OfAKind = "of_a_kind";
        public const string Straight = "straight";
        public const string ThreePairs = "three_pairs";
    }

    /// <summary>
    /// Одна комбинация из отложенных костей: сколько костей каждой грани она забирает и сколько стоит.
    /// </summary>
    public sealed class ScoringCombo
    {
        public ScoringCombo(string category, int[] used, int score)
        {
            Category = category;
            Used = used;
            Score = score;
        }

        public string Category { get; }

        /// <summary>Сколько костей каждой грани забирает комбинация. Индекс = грань 1..6, [0] не используется.</summary>
        public int[] Used { get; }

        /// <summary>Базовые очки по правилам, до модификаторов.</summary>
        public int Score { get; }

        public int DiceCount
        {
            get
            {
                var count = 0;
                for (var face = 1; face <= 6; face++)
                    count += Used[face];
                return count;
            }
        }
    }
}
