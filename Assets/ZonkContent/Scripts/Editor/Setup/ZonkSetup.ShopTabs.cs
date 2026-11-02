using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Вкладки магазина — лента с прокруткой (07.10.2026): вкладок стало 16, в одну строку равной ширины длинные слова
    /// сжимались («Запястье») или ломались («Безделушк-и»). Теперь ширина каждой — по слову (ShopWindow.FitTab), шрифт
    /// один, лента листается пальцем и колесом, «Назад» — справа вне ленты.
    /// </summary>
    public static partial class ZonkSetup
    {
        /// <summary>Лента вкладок: горизонтальная прокрутка без подложки и отступов, вкладки по высоте ленты.</summary>
        private static RectTransform TabStrip(RectTransform parent, out RectTransform content)
        {
            var root = HorizontalList("Tabs", parent, 8, out content);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            content.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            var scroll = root.GetComponent<ScrollRect>();
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            return root;
        }

        /// <summary>
        /// Окно магазина, созданное раньше (вкладки в ряду растягиваются): вкладки переезжают в ленту с прокруткой.
        /// Заблокированное окно не трогается; уже переделанное — тоже.
        /// </summary>
        private static void UpgradeShopTabs()
        {
            var path = Prefabs + "/UI/ShopWindow.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || SkipIfLocked(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var window = root.GetComponent<ShopWindow>();
                var serialized = window != null ? new SerializedObject(window) : null;
                var tabs = serialized?.FindProperty("_tabs").objectReferenceValue as RectTransform;
                var back = serialized?.FindProperty("_back").objectReferenceValue as Component;
                if (tabs == null || tabs.GetComponentInParent<ScrollRect>() != null)
                    return;

                var row = tabs.GetComponent<HorizontalLayoutGroup>();
                if (row != null)
                    row.childForceExpandWidth = false;
                var strip = TabStrip(tabs, out var content);
                strip.SetSiblingIndex(0);
                Width(strip, -1, 1);

                // Шаблон вкладки и уже лежащие вкладки — в ленту; «Назад» остаётся в ряду, справа.
                for (var i = tabs.childCount - 1; i >= 0; i--)
                {
                    var child = tabs.GetChild(i);
                    if (child == strip || (back != null && child == back.transform))
                        continue;
                    child.SetParent(content, false);
                    child.SetAsFirstSibling();
                }

                if (back != null)
                {
                    back.transform.SetAsLastSibling();
                    Width(back, 170, 0);
                }

                serialized.FindProperty("_tabs").objectReferenceValue = content;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Zonk] ShopWindow: tabs moved into a scrolling strip");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
