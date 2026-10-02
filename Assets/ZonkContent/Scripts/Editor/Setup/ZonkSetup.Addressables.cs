using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Загрузка по требованию (Addressables). Префабы косметики (локации, столы, стаканы, лампы) — каждый своим
    /// пакетом в группе CosmeticsGroup: в памяти и в загрузке игры только то, что стоит на столе. Общее у нескольких
    /// префабов (шейдер, общие материалы) выносится в отдельную группу, чтобы не дублироваться в каждом пакете.
    ///
    /// Пакеты собираются вместе с игрой (Build Addressables on Player Build): меню Base/Build и окно Build
    /// работают как раньше; в браузере пакеты лежат рядом со сборкой (StreamingAssets) и скачиваются по мере надобности.
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string CosmeticsGroup = "Zonk Cosmetics";

        /// <summary>Настройки Addressables (создаются при первом вызове) с локальной сборкой вместе с игрой.</summary>
        private static AddressableAssetSettings AddressableSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings.BuildAddressablesWithPlayerBuild != AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer)
            {
                settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
                EditorUtility.SetDirty(settings);
            }

            return settings;
        }

        private static AddressableAssetGroup AddressableGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name);
            if (group == null)
            {
                group = settings.CreateGroup(name, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }

            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled != null && bundled.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
            {
                // Каждый предмет — свой пакет: загрузка одной локации не тянет остальные.
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                EditorUtility.SetDirty(group);
            }

            return group;
        }

        /// <summary>Сделать префаб загружаемым по требованию и вернуть ссылку на него.</summary>
        private static AssetReferenceGameObject MakeAddressable(AddressableAssetSettings settings, AddressableAssetGroup group,
            GameObject prefab)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = Path.GetFileNameWithoutExtension(path);
            return new AssetReferenceGameObject(guid);
        }

        /// <summary>
        /// Все предметы с прямой ссылкой на префаб (PrefabPayload.Prefab) переводятся на загрузку по требованию:
        /// префаб помечается Addressable, ссылка переносится в Asset, прямая очищается. Затем общие зависимости
        /// выносятся в отдельную группу.
        /// </summary>
        private static void MigratePrefabPayloads()
        {
            var settings = AddressableSettings();
            var group = AddressableGroup(settings, CosmeticsGroup);
            var moved = 0;
            foreach (var item in FindAll<CosmeticItemConfig>(ConfigsFolder))
            {
                if (!(item.Payload is PrefabPayload payload) || payload.Prefab == null)
                    continue;

                payload.Asset = MakeAddressable(settings, group, payload.Prefab);
                payload.Prefab = null;
                EditorUtility.SetDirty(item);
                moved++;
            }

            if (moved > 0)
            {
                Debug.Log($"[Zonk] Prefabs moved to on-demand loading (Addressables): {moved}");
                AssetDatabase.SaveAssets();
            }

            IsolateDuplicates(settings);
        }

        /// <summary>Общее у нескольких пакетов (шейдер, материалы) — в отдельную группу, иначе копия в каждом пакете.</summary>
        private static void IsolateDuplicates(AddressableAssetSettings settings)
        {
            var rule = new CheckBundleDupeDependencies();
            var results = rule.RefreshAnalysis(settings);
            if (results.Count > 0 && !(results.Count == 1 && results[0].resultName == "No issues found"))
                rule.FixIssues(settings);
            rule.ClearAnalysis();
        }

        /// <summary>Префаб предмета в редакторе: по ссылке Addressables или прямой (для предпросмотра и проверок).</summary>
        public static GameObject EditorPrefabOf(PrefabPayload payload)
        {
            if (payload == null)
                return null;
            if (payload.Prefab != null)
                return payload.Prefab;
            return payload.Asset != null ? payload.Asset.editorAsset : null;
        }
    }
}
