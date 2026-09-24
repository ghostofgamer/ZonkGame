namespace Base.Services.Quality
{
    /// <summary>
    /// Уровень качества графики. Игра опирается только на него,
    /// а что конкретно меняется на каждом уровне, решают Quality Levels в Project Settings.
    /// </summary>
    public enum QualityTier
    {
        /// <summary>Телефоны и слабые устройства.</summary>
        Low = 0,
        /// <summary>Планшеты.</summary>
        Medium = 1,
        /// <summary>ПК и телевизоры.</summary>
        High = 2,
    }
}
