using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Магазин, правая колонка (05.10.2026, по отзыву): у безделушки кнопок мест, действия и подсказок больше, чем
    /// вмещает колонка, а колонка выровнена по центру строки — лишнее вылезало и вверх (название на вкладки), и вниз
    /// (за экран). Теперь описание — прокрутка, а места — одной строкой (шаблон SpotRowTemplate). Дописывается в уже
    /// созданный префаб один раз (заблокированный не трогается): без шаблона окно работает по-старому.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static void UpgradeShopDetails()
        {
            var path = Prefabs + "/UI/ShopWindow.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || SkipIfLocked(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var window = root.GetComponent<ShopWindow>();
                var details = FindDeep(root.transform, "Details") as RectTransform;
                if (window == null || window.HasSpotRow || details == null || !(details.parent is RectTransform column))
                    return;

                // Прокрутка на место списка: занимает остаток колонки под названием.
                var scrollRoot = ScrollBox("DetailsScroll", column, out var scroll, out var content);
                scrollRoot.SetSiblingIndex(details.GetSiblingIndex());
                var layout = LayoutOf(scrollRoot);
                layout.minHeight = 0f;
                layout.flexibleHeight = 1f;
                scroll.horizontal = false;

                // Список описания становится содержимым прокрутки: прижат к верху, растёт вниз по содержимому.
                var viewport = (RectTransform)content.parent;
                Object.DestroyImmediate(content.gameObject);
                details.SetParent(viewport, false);
                details.anchorMin = new Vector2(0f, 1f);
                details.anchorMax = new Vector2(1f, 1f);
                details.pivot = new Vector2(0.5f, 1f);
                details.anchoredPosition = Vector2.zero;
                details.sizeDelta = Vector2.zero;
                var fitter = details.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                    fitter = details.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                scroll.content = details;

                // Строка мест: кнопки делят ширину поровну.
                var spotRow = Row(details, 8);
                spotRow.name = "SpotRowTemplate";
                Height(spotRow, 56);
                spotRow.gameObject.SetActive(false);

                window.EditorSetupSpotRow(spotRow);
                EditorUtility.SetDirty(window);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Zonk] ShopWindow: scrollable details and spot row added");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
