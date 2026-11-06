using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Иконки магазина: каждый предмет снимается камерой в едином ракурсе, снимок — картинка в Art/Sprites/Icons.
    /// Ручная иконка (из другой папки) не заменяется. Новый предмет получает иконку повторным запуском меню.
    /// Съёмка идёт в отдельной сцене редактора, открытая сцена не меняется.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string IconsFolder = Root + "/Art/Sprites/Icons";
        private const int IconRender = 384;
        private const int IconMaxSize = 192;

        private const string EnvironmentPrefix = "env_";
        private const float ItemIconFov = 22f;
        private static readonly int BakedDarkenId = Shader.PropertyToID("_ZonkBakedDarken");

        // Локация снимается изнутри, как открытка: самое узнаваемое место сцены. Сцена огромна (небо, холмы), поэтому общий кадр вышел бы пустым.
        // Варианты (env_tavern_night и другие) снимаются с того же места: начало имени совпадает. Стола в префабе нет, центр свободен.
        private static readonly (string Id, Vector3 Position, Vector3 Target, float Fov)[] EnvironmentShots =
        {
            ("env_home", new Vector3(-0.8f, 1.5f, -5.6f), new Vector3(1.2f, -0.1f, 4.2f), 52f),
            ("env_tavern", new Vector3(0.6f, 1.5f, -5.6f), new Vector3(-0.2f, -0.1f, 4.2f), 54f),
            ("env_beach", new Vector3(2.8f, 1.3f, -7.2f), new Vector3(-1.5f, -0.1f, 3.4f), 50f),
            ("env_ship", new Vector3(0.6f, 1.3f, -5.2f), new Vector3(0.4f, 0.5f, 6f), 60f),
            ("env_garden", new Vector3(1.4f, 0.9f, -6.2f), new Vector3(0.6f, -0.1f, 6.5f), 54f),
            ("env_attic", new Vector3(0f, 0.7f, -5.2f), new Vector3(0f, 0.75f, 5.5f), 60f),
            ("env_basement", new Vector3(0f, 0.6f, -5.2f), new Vector3(0f, 0.75f, 5.5f), 62f),
        };

        [MenuItem("Zonk/Art/Bake Shop Icons", priority = 45)]
        public static void BakeShopIconsMenu()
        {
            var baked = BakeShopIcons(out var skipped);
            Debug.Log("[Zonk] Shop icons baked: " + baked + ". Without a model (no icon): " + string.Join(", ", skipped));
        }

        /// <summary>Только локации: принятые иконки предметов не переснимаются.</summary>
        [MenuItem("Zonk/Art/Bake Location Icons", priority = 45)]
        public static void BakeLocationIconsMenu()
        {
            var baked = BakeShopIcons(out var skipped, EnvironmentPrefix);
            Debug.Log("[Zonk] Location icons baked: " + baked + ". Without a model (no icon): " + string.Join(", ", skipped));
        }

        /// <summary>Только облик руки (кисти, рукава, запястье): после пересборки моделей в Blender, остальные иконки не трогаются.</summary>
        [MenuItem("Zonk/Art/Bake Hand Look Icons", priority = 45)]
        public static void BakeHandLookIconsMenu()
        {
            var baked = BakeShopIcons(out var skipped, "hand_", "sleeve_", "wrist_");
            Debug.Log("[Zonk] Hand look icons baked: " + baked + ". Without a model (no icon): " + string.Join(", ", skipped));
        }

        /// <param name="onlyPrefixes">Снимать только предметы с ID на эти приставки; пусто — все.</param>
        public static int BakeShopIcons(out List<string> skipped, params string[] onlyPrefixes)
        {
            skipped = new List<string>();
            EnsureFolder(IconsFolder);

            var dieMesh = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Dice/Die.prefab")?.GetComponentInChildren<MeshFilter>(true)?.sharedMesh;
            var scene = EditorSceneManager.NewPreviewScene();
            var target = RenderTexture.GetTemporary(IconRender, IconRender, 24, RenderTextureFormat.ARGB32);
            var baked = 0;
            var written = new List<KeyValuePair<Object, string>>();
            try
            {
                var camera = IconCamera(scene, target);
                IconLight(scene);

                foreach (var guid in AssetDatabase.FindAssets("t:ContentConfig", new[] { ConfigsFolder }))
                {
                    var config = AssetDatabase.LoadAssetAtPath<ContentConfig>(AssetDatabase.GUIDToAssetPath(guid));
                    if (!NeedsIcon(config) || !MatchesPrefix(config.Id, onlyPrefixes))
                        continue;

                    var subject = IconSubject(config, dieMesh, scene, out var flat);
                    if (subject == null)
                    {
                        if (config is CosmeticItemConfig)
                            skipped.Add(config.Id);
                        continue;
                    }

                    try
                    {
                        if (!FrameEnvironment(camera, subject, config))
                            Frame(camera, subject, flat);

                        camera.Render();
                        ResetIconCamera(camera);
                        var path = IconsFolder + "/" + config.Id + ".png";
                        WriteRender(target, path);
                        written.Add(new KeyValuePair<Object, string>(config, path));
                        baked++;
                    }
                    finally
                    {
                        Object.DestroyImmediate(subject);
                    }
                }
            }
            finally
            {
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.Refresh();
            foreach (var entry in written)
                AssignIcon(entry.Key, entry.Value);
            AssetDatabase.SaveAssets();
            return baked;
        }

        /// <summary>Иконка пуста или снята раньше этим же меню. Ручную из другой папки не трогаем.</summary>
        private static bool MatchesPrefix(string id, string[] prefixes)
        {
            if (prefixes == null || prefixes.Length == 0)
                return true;
            foreach (var prefix in prefixes)
                if (id.StartsWith(prefix))
                    return true;
            return false;
        }

        private static bool NeedsIcon(ContentConfig config)
        {
            Sprite icon;
            switch (config)
            {
                case CosmeticItemConfig cosmetic: icon = cosmetic.Icon; break;
                case DieConfig die: icon = die.Icon; break;
                default: return false;
            }

            return icon == null || AssetDatabase.GetAssetPath(icon).StartsWith(IconsFolder);
        }

        private static Camera IconCamera(Scene scene, RenderTexture target)
        {
            var go = new GameObject("IconCamera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>();
            camera.scene = scene;
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = ItemIconFov;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 500f;
            return camera;
        }

        private static void IconLight(Scene scene)
        {
            var go = new GameObject("IconLight");
            SceneManager.MoveGameObjectToScene(go, scene);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.color = Color.white;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }

        /// <summary>Что снимать: модель предмета, кость с материалом или кусок ткани. flat — смотреть сверху.</summary>
        private static GameObject IconSubject(ContentConfig config, Mesh dieMesh, Scene scene, out bool flat)
        {
            flat = false;
            GameObject subject = null;
            switch (config)
            {
                case DieConfig die:
                    subject = MeshSubject("Die", die.LookMesh != null ? die.LookMesh : dieMesh, die.LookMaterial, die.LookAccent);
                    break;

                case CosmeticItemConfig cosmetic:
                    switch (cosmetic.Payload)
                    {
                        case PrefabPayload prefab:
                            var source = prefab.Prefab != null ? prefab.Prefab : prefab.Asset != null ? prefab.Asset.editorAsset : null;
                            if (source != null)
                                subject = Object.Instantiate(source);
                            break;
                        case MeshMaterialPayload mesh:
                            subject = MeshSubject("Skin", mesh.Mesh != null ? mesh.Mesh : dieMesh, mesh.Material, null);
                            break;
                        case MaterialPayload material:
                            subject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                            Object.DestroyImmediate(subject.GetComponent<Collider>());
                            subject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                            subject.GetComponent<MeshRenderer>().sharedMaterial = material.Material;
                            flat = true;
                            break;
                    }

                    break;
            }

            if (subject != null)
            {
                subject.SetActive(true);
                SceneManager.MoveGameObjectToScene(subject, scene);
            }

            return subject;
        }

        private static GameObject MeshSubject(string name, Mesh mesh, Material material, Material accent)
        {
            if (mesh == null || material == null)
                return null;

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = accent != null ? new[] { material, accent } : new[] { material };
            // Единица, двойка и тройка видны сразу: узнаётся как кость.
            go.transform.rotation = Quaternion.Euler(-12f, 28f, -8f);
            return go;
        }

        /// <summary>
        /// Ракурс локации изнутри. Свет профиля: ночной вариант темнее на той же модели, поэтому иконки отличаются.
        /// Пятна-ореолы огня прячем: их цвет выставляет компонент в игре, в снимке они были бы белыми кругами.
        /// </summary>
        private static bool FrameEnvironment(Camera camera, GameObject subject, ContentConfig config)
        {
            if (!(config is CosmeticItemConfig cosmetic) || !config.Id.StartsWith(EnvironmentPrefix))
                return false;

            foreach (var shot in EnvironmentShots)
            {
                if (!config.Id.StartsWith(shot.Id))
                    continue;

                foreach (var glow in subject.GetComponentsInChildren<Zonk.Presentation.WorldGlowPulse>(true))
                    glow.gameObject.SetActive(false);

                var lighting = cosmetic.Payload is PrefabPayload prefab ? prefab.Lighting : null;
                Shader.SetGlobalColor(BakedDarkenId, lighting != null ? Color.white - lighting.BakedTint : Color.clear);
                camera.backgroundColor = lighting != null ? new Color(lighting.Background.r, lighting.Background.g, lighting.Background.b, 1f) : Color.black;
                camera.fieldOfView = shot.Fov;
                camera.transform.position = shot.Position;
                camera.transform.rotation = Quaternion.LookRotation(shot.Target - shot.Position, Vector3.up);
                return true;
            }

            return false;
        }

        /// <summary>Вернуть настройки предметов и обычный вид запечённых комнат в редакторе.</summary>
        private static void ResetIconCamera(Camera camera)
        {
            camera.fieldOfView = ItemIconFov;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            Shader.SetGlobalColor(BakedDarkenId, Color.clear);
        }

        /// <summary>Камера так, чтобы весь предмет вошёл в кадр с небольшим полем.</summary>
        private static void Frame(Camera camera, GameObject subject, bool flat)
        {
            var renderers = subject.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            var direction = flat ? new Vector3(0f, 1f, -0.001f).normalized : new Vector3(-0.55f, 0.55f, -1f).normalized;
            var radius = Mathf.Max(0.001f, bounds.extents.magnitude);
            var distance = radius * 1.08f / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.rotation = Quaternion.LookRotation(-direction, flat ? Vector3.forward : Vector3.up);
        }

        private static void WriteRender(RenderTexture source, string path)
        {
            RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        private static void AssignIcon(Object config, string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = IconMaxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = true;
                importer.compressionQuality = 60;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            switch (config)
            {
                case CosmeticItemConfig cosmetic: cosmetic.Icon = sprite; break;
                case DieConfig die: die.Icon = sprite; break;
            }

            EditorUtility.SetDirty(config);
        }
    }
}
