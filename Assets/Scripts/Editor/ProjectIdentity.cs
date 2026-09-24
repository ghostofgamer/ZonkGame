using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Base.Editor
{
    /// <summary>
    /// Имя компании, название и идентификатор приложения в одном месте.
    /// Выставляются в PlayerSettings при переключении площадки и перед каждой сборкой,
    /// поэтому для новой игры на основе шаблона достаточно поменять константы здесь
    /// (полный список того, что меняется в новой игре, в README, раздел «Новая игра из шаблона»).
    ///
    /// Идентификатор Android-пакета после первой публикации в магазине менять нельзя:
    /// для магазина это уже другое приложение.
    /// </summary>
    public static class ProjectIdentity
    {
        public const string CompanyName = "ghostofgamer";

        /// <summary>Название игры: подпись под иконкой на Android и заголовок вкладки в браузере. Можно по-русски.</summary>
        public const string ProductName = "Зонк: Кости Фортуны";

        /// <summary>Имя файла Android-сборки (Builds/RuStore/&lt;имя&gt;.apk). Латиница без пробелов.</summary>
        public const string BuildFileName = "Zonk";

        public const string AndroidPackage = "ru.ghostofgamer.zonk";

        public static void Apply()
        {
            if (PlayerSettings.companyName != CompanyName)
            {
                Debug.Log($"[Base] Company name: {PlayerSettings.companyName} -> {CompanyName}");
                PlayerSettings.companyName = CompanyName;
            }

            if (PlayerSettings.productName != ProductName)
            {
                Debug.Log($"[Base] Product name: {PlayerSettings.productName} -> {ProductName}");
                PlayerSettings.productName = ProductName;
            }

            var current = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (current != AndroidPackage)
            {
                Debug.Log($"[Base] Android package: {current} -> {AndroidPackage}");
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackage);
            }
        }
    }
}
