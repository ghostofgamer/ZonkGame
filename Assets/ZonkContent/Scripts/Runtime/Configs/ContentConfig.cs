using UnityEngine;

namespace Zonk.Configs
{
    /// <summary>
    /// Любой контент игры: кость, скин, стакан, соперник, глава. У каждого строковый ID, он хранится
    /// в сохранении и не меняется после выпуска. Новый контент = новый ассет, реестр (ContentDatabase)
    /// собирается сам (меню Zonk/Content/Rebuild Database), валидатор проверяет ID и ссылки.
    ///
    /// Конфиги — контейнеры данных: поля публичные, в игре только читаются.
    /// </summary>
    public abstract class ContentConfig : ScriptableObject
    {
        [Tooltip("Постоянный ID для сохранений и сети. После выпуска не менять.")]
        public string Id;

        [Tooltip("Ключ названия в таблице локализации")]
        public string NameKey;

        public override string ToString()
        {
            return $"{GetType().Name}({Id})";
        }
    }
}
