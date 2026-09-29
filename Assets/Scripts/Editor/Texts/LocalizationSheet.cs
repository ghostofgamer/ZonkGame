using UnityEngine;

namespace Base.Editor.Texts
{
    /// <summary>
    /// Связь CSV-файла в проекте с Google Таблицей. Создать: Create → Base → Localization Sheet.
    /// В таблице: Файл → Поделиться → Опубликовать в интернете → нужный лист → CSV, ссылку вставить сюда.
    /// Меню Base/Localization/Download Sheets скачивает все такие листы в их CSV-файлы.
    /// </summary>
    [CreateAssetMenu(menuName = "Base/Localization Sheet", fileName = "LocalizationSheet")]
    public sealed class LocalizationSheet : ScriptableObject
    {
        [Tooltip("Ссылка на опубликованный лист в формате CSV (…/pub?gid=…&single=true&output=csv)")]
        public string PublishedCsvUrl;

        [Tooltip("CSV-файл в проекте, который перезаписывается при скачивании")]
        public TextAsset Target;
    }
}
