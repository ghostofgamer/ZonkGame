using System.Collections.Generic;
using Base.Core.Localization;
using UnityEngine;

namespace Base.Services.Texts
{
    /// <summary>
    /// Тексты из CSV-файла в проекте (TextAsset). Файл скачивается из Google Таблицы
    /// меню Base/Localization/Download Sheets. Игра биндит источник в своём инсталлере:
    /// Container.Bind&lt;ILocalizationSource&gt;().FromInstance(new TextAssetLocalizationSource(csv)).
    /// </summary>
    public sealed class TextAssetLocalizationSource : ILocalizationSource
    {
        private readonly TextAsset _csv;

        public TextAssetLocalizationSource(TextAsset csv)
        {
            _csv = csv;
        }

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load()
        {
            if (_csv == null)
            {
                Debug.LogError("[Localization] CSV asset is not assigned");
                return new Dictionary<string, IReadOnlyDictionary<string, string>>();
            }

            return CsvLocalizationParser.Parse(_csv.text);
        }
    }
}
