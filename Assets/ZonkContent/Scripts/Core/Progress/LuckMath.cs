using Zonk.Core.Rules;

namespace Zonk.Core.Progress
{
    /// <summary>
    /// «Удача» для статистики: сколько Зонков было бы у честных обычных костей. Вероятность Зонка при броске
    /// n обычных костей считается по классическим правилам перебором раскладов граней (до 462 на шесть костей)
    /// один раз. Сравнивается только с бросками, где все брошенные кости обычные: у особых свои веса граней.
    /// </summary>
    public static class LuckMath
    {
        /// <summary>Доля единиц и пятёрок на честной кости.</summary>
        public const double OnesAndFivesShare = 2.0 / 6.0;

        private static double[] _zonkChance;

        /// <summary>Вероятность Зонка при броске count обычных костей (1..6), 0 вне диапазона.</summary>
        public static double ZonkChance(int count)
        {
            if (count < 1 || count > 6)
                return 0;

            if (_zonkChance == null)
                _zonkChance = Compute(new ScoreCalculator(RuleSet.CreateClassicRules()));
            return _zonkChance[count];
        }

        /// <summary>Вероятности Зонка для 1..6 костей по данным правилам.</summary>
        public static double[] Compute(ScoreCalculator calculator)
        {
            var result = new double[7];
            var counts = new int[7];
            for (var dice = 1; dice <= 6; dice++)
            {
                var total = Pow6(dice);
                var zonks = CountZonks(calculator, counts, 1, dice, Factorial(dice));
                result[dice] = zonks / total;
            }

            return result;
        }

        /// <summary>Сумма числа исходов раскладов без очков: грани face..6 делят оставшиеся left костей.</summary>
        private static double CountZonks(ScoreCalculator calculator, int[] counts, int face, int left, double ways)
        {
            if (face == 6)
            {
                counts[6] = left;
                var outcomes = ways / Factorial(left);
                var zonk = !calculator.HasAnyScoreCounts(counts);
                counts[6] = 0;
                return zonk ? outcomes : 0;
            }

            var sum = 0.0;
            for (var take = 0; take <= left; take++)
            {
                counts[face] = take;
                sum += CountZonks(calculator, counts, face + 1, left - take, ways / Factorial(take));
            }

            counts[face] = 0;
            return sum;
        }

        private static double Factorial(int n)
        {
            var result = 1.0;
            for (var i = 2; i <= n; i++)
                result *= i;
            return result;
        }

        private static double Pow6(int n)
        {
            var result = 1.0;
            for (var i = 0; i < n; i++)
                result *= 6;
            return result;
        }
    }
}
