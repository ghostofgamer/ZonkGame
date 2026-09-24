namespace Base.Services.Saves
{
    /// <summary>
    /// Перевод раздела сохранения из версии FromVersion в FromVersion + 1.
    /// Игра биндит миграции в своём инсталлере: Container.Bind&lt;ISaveMigration&gt;().To&lt;...&gt;().AsSingle().
    /// Первая миграция раздела имеет FromVersion = 1, следующая 2 и так далее.
    /// Удалять старые миграции нельзя: игрок мог не заходить в игру несколько обновлений.
    /// </summary>
    public interface ISaveMigration
    {
        /// <summary>Ключ раздела, как в ISaveStore.Get.</summary>
        string Key { get; }

        /// <summary>Версия, из которой переводит миграция.</summary>
        int FromVersion { get; }

        /// <summary>Принимает JSON раздела версии FromVersion, возвращает JSON версии FromVersion + 1.</summary>
        string Migrate(string json);
    }
}
