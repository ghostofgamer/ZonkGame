using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zonk.UI.Views;
using Zonk.UI.Windows;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Починка окна профиля: страницы «Таланты» и «Уровни» лежат в префабе, но ссылки окна на них обнулились
    /// (06.11.2026: вкладок не было видно). Генератор такие окна пропускал — кнопка вкладки уже стояла.
    /// Здесь ссылки находятся по именам объектов и ставятся заново; вёрстка не меняется.
    /// </summary>
    public static partial class ZonkSetup
    {
        [MenuItem("Zonk/Setup/Repair Profile Tabs", priority = 8)]
        public static void RepairProfileTabsMenu()
        {
            Debug.Log("[Zonk] Profile tabs: " + RepairProfileTabs());
        }

        private static string RepairProfileTabs()
        {
            var path = Prefabs + "/UI/ProfileWindow.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return "no prefab";

            var root = PrefabUtility.LoadPrefabContents(path);
            var result = new List<string>();
            try
            {
                var window = root.GetComponent<ProfileWindow>();
                if (window == null)
                    return "no ProfileWindow";

                var serialized = new SerializedObject(window);
                var pages = FindDeep(root.transform, "Pages");
                if (pages == null)
                    return "no Pages";

                if (serialized.FindProperty("_talentsPage").objectReferenceValue == null)
                    result.Add(RelinkTalents(window, serialized, pages));
                if (serialized.FindProperty("_levelsPage").objectReferenceValue == null)
                    result.Add(RelinkLevels(window, serialized, pages));

                if (result.Count > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                return result.Count > 0 ? string.Join("; ", result) : "ok, nothing to repair";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string RelinkTalents(ProfileWindow window, SerializedObject serialized, Transform pages)
        {
            var page = pages.Find("Talents");
            var tab = serialized.FindProperty("_talentsTab").objectReferenceValue as UiButtonView;
            if (page == null || tab == null)
                return "talents: page or tab missing";

            var titles = new List<TMP_Text>();
            var columns = new List<RectTransform>();
            foreach (var scroll in page.GetComponentsInChildren<ScrollRect>(true))
            {
                if (scroll.content == null || scroll.name != "Nodes")
                    continue;
                columns.Add(scroll.content);
                var title = scroll.transform.parent != null ? scroll.transform.parent.Find("Title") : null;
                titles.Add(title != null ? title.GetComponent<TMP_Text>() : null);
            }

            var points = FindDeep(page, "Points")?.GetComponent<TMP_Text>();
            var info = FindDeep(page, "Info")?.GetComponent<TMP_Text>();
            var learn = FindDeep(page, "Learn")?.GetComponent<UiButtonView>();
            var reset = FindDeep(page, "Reset")?.GetComponent<UiButtonView>();
            if (columns.Count < 3 || points == null || info == null || learn == null || reset == null)
                return "talents: page incomplete (columns " + columns.Count + ")";

            window.EditorSetupTalents(tab, page.gameObject, points, titles.ToArray(), columns.ToArray(), TalentNodePart(), info, learn,
                reset, TalentIcons());
            EditorUtility.SetDirty(window);
            return "talents relinked";
        }

        private static string RelinkLevels(ProfileWindow window, SerializedObject serialized, Transform pages)
        {
            var page = pages.Find("Levels");
            var tab = serialized.FindProperty("_levelsTab").objectReferenceValue as UiButtonView;
            if (page == null || tab == null)
                return "levels: page or tab missing";

            var summary = FindDeep(page, "Summary")?.GetComponent<TMP_Text>();
            var scroll = page.GetComponentInChildren<ScrollRect>(true);
            if (summary == null || scroll == null || scroll.content == null)
                return "levels: page incomplete";

            window.EditorSetupLevels(tab, page.gameObject, summary, scroll.content, LevelRewardRowPart());
            EditorUtility.SetDirty(window);
            return "levels relinked";
        }
    }
}
