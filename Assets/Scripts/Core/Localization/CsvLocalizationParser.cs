using System;
using System.Collections.Generic;
using System.Text;

namespace Base.Core.Localization
{
    /// <summary>
    /// Разбор CSV с текстами. Первая строка: заголовок "key,ru,en,...". Столбцы с другими именами
    /// (например, "comment") пропускаются. Пустая ячейка перевода не попадает в таблицу, и Localization
    /// откатывается на английский. Строки с пустым ключом или ключом на "#" пропускаются: это разделы.
    ///
    /// Формат совпадает с выгрузкой Google Таблиц (Файл → Скачать → CSV или «Опубликовать в интернете»):
    /// запятая как разделитель, кавычки вокруг ячеек с запятыми и переносами, "" внутри кавычек.
    /// </summary>
    public static class CsvLocalizationParser
    {
        public const string KeyColumn = "key";

        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Parse(string csv)
        {
            return Parse(csv, out _);
        }

        /// <summary>missing: ключи, у которых нет перевода хотя бы на один язык из заголовка.</summary>
        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Parse(string csv, out List<string> missing)
        {
            var table = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            missing = new List<string>();

            var rows = ReadRows(csv ?? string.Empty);
            if (rows.Count == 0)
                return table;

            var header = rows[0];
            var keyIndex = -1;
            var languages = new Dictionary<int, string>();
            for (var i = 0; i < header.Count; i++)
            {
                var name = header[i].Trim().ToLowerInvariant();
                if (name == KeyColumn)
                    keyIndex = i;
                else if (IsLanguageCode(name))
                    languages[i] = name;
            }

            if (keyIndex < 0)
                throw new FormatException("Localization CSV has no 'key' column");

            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (keyIndex >= row.Count)
                    continue;

                var key = row[keyIndex].Trim();
                if (key.Length == 0 || key.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var translations = new Dictionary<string, string>();
                foreach (var language in languages)
                {
                    var text = language.Key < row.Count ? row[language.Key] : string.Empty;
                    if (text.Length > 0)
                        translations[language.Value] = text;
                    else if (!missing.Contains(key))
                        missing.Add(key);
                }

                table[key] = translations;
            }

            return table;
        }

        /// <summary>Двухбуквенный код ISO 639-1: "ru", "en", "tr".</summary>
        private static bool IsLanguageCode(string name)
        {
            return name.Length == 2 && char.IsLetter(name[0]) && char.IsLetter(name[1]);
        }

        private static List<List<string>> ReadRows(string csv)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < csv.Length; i++)
            {
                var c = csv[i];

                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            // Первая ячейка файла из Excel может начинаться с BOM.
            if (rows.Count > 0 && rows[0].Count > 0)
                rows[0][0] = rows[0][0].TrimStart('﻿');

            return rows;
        }
    }
}
