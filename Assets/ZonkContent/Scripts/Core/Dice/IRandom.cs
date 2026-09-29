namespace Zonk.Core.Dice
{
    /// <summary>
    /// Источник случайных чисел. Все броски партии идут через него, поэтому партия с тем же сидом
    /// повторяется один в один: для тестов, симулятора баланса и будущего онлайна.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Число в диапазоне [0, 1).</summary>
        double NextDouble();

        /// <summary>Целое в диапазоне [0, maxExclusive).</summary>
        int Next(int maxExclusive);
    }
}
