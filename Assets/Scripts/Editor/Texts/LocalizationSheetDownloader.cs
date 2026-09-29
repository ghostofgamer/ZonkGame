using System;
using System.IO;
using System.Net.Http;
using System.Text;
using Base.Core.Localization;
using UnityEditor;
using UnityEngine;

namespace Base.Editor.Texts
{
    /// <summary>
    /// Скачивает тексты из Google Таблиц в CSV-файлы проекта. Перед записью CSV разбирается:
    /// сломанный файл (нет столбца key, сеть вернула страницу входа) не затирает рабочий.
    /// Ключи без перевода выводятся предупреждением.
    /// </summary>
    public static class LocalizationSheetDownloader
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        [MenuItem("Base/Localization/Download Sheets", priority = 30)]
        public static void DownloadAll()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(LocalizationSheet));
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Localization] No LocalizationSheet assets. Create one: Create → Base → Localization Sheet");
                return;
            }

            using (var client = new HttpClient { Timeout = Timeout })
            {
                foreach (var guid in guids)
                {
                    var sheet = AssetDatabase.LoadAssetAtPath<LocalizationSheet>(AssetDatabase.GUIDToAssetPath(guid));
                    Download(client, sheet);
                }
            }

            AssetDatabase.Refresh();
        }

        private static void Download(HttpClient client, LocalizationSheet sheet)
        {
            if (string.IsNullOrWhiteSpace(sheet.PublishedCsvUrl) || sheet.Target == null)
            {
                Debug.LogError($"[Localization] {sheet.name}: URL or target CSV is not set", sheet);
                return;
            }

            string csv;
            try
            {
                var bytes = client.GetByteArrayAsync(sheet.PublishedCsvUrl).GetAwaiter().GetResult();
                csv = Encoding.UTF8.GetString(bytes);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Localization] {sheet.name}: download failed: {e.Message}", sheet);
                return;
            }

            try
            {
                var table = CsvLocalizationParser.Parse(csv, out var missing);
                if (table.Count == 0)
                    throw new FormatException("no rows");

                var path = AssetDatabase.GetAssetPath(sheet.Target);
                File.WriteAllText(path, csv, new UTF8Encoding(false));
                Debug.Log($"[Localization] {sheet.name}: {table.Count} keys -> {path}");

                if (missing.Count > 0)
                    Debug.LogWarning($"[Localization] {sheet.name}: no translation for {missing.Count} keys: {string.Join(", ", missing)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Localization] {sheet.name}: downloaded file is not a valid sheet ({e.Message}). " +
                               "Check that the sheet is published as CSV.", sheet);
            }
        }
    }
}
