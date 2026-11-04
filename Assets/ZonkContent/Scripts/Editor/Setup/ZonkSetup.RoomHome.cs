using UnityEditor;
using UnityEngine;

using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// «Родной дом» со светом, запечённым в Tools/Blender/zonk_room.py: модель Room_Home и текстуры Home_Room, Home_Props.
    /// Источников света в комнате нет, материалы без расчёта света, поэтому дёшево для телефонов.
    /// Живые части лежат поверх: огонь, свечи, дымок, отсвет на полу. Префаб Env_Home остаётся тем же файлом, ссылка на него не меняется.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string RoomModel = Root + "/Art/Models/Environments/Room_Home.fbx";
        private const string RoomTextures = Root + "/Art/Textures/Environments";

        // Положения те же, что в zonk_room.py (единицы Unity): камин, его полка.
        private const float RoomFloor = -1.6f;
        private const float FireX = 4.5f;
        private const float FireZ = 4.58f;

        // Запечённые текстуры: качество Crunch, без анизотропии, сдвиг мипов в сторону резкости.
        private const int BakedTextureQuality = 95;
        private const int BakedTextureAniso = 1;
        private const float BakedTextureMipBias = -0.7f;

        [MenuItem("Zonk/Art/Apply Home Room", priority = 46)]
        public static void ApplyHomeRoomMenu()
        {
            AssetDatabase.Refresh();
            ImportComicSprites();
            ApplyHomeRoom();
            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Home room applied");
        }

        public static void ApplyHomeRoom()
        {
            var path = Prefabs + "/Environments/Env_Home.prefab";
            var room = BakedMaterial("M_Home_Room", "Home_Room", 2048);
            var props = BakedMaterial("M_Home_Props", "Home_Props", 1024);
            var smoke = SmokeWispMaterial();
            ApplyBakedRoom(path, RoomModel, "zonk_room.py", room, props, root =>
            {
                AddFlames(root, FireX, FireZ);
                AddCandle(root, new Vector3(FireX - 1.35f, RoomFloor + 2.68f, 4.5f));
                AddCandle(root, new Vector3(FireX + 1.25f, RoomFloor + 2.68f, 4.5f));
                AddSmoke(root, smoke, FireX, FireZ);
                AddFloorGlow(root, FireX, 2.9f);
            });

            TuneHomeLighting();
        }

        /// <summary>Запечённая комната в префаб локации: прежнее содержимое заменяется, сам файл и ссылки на него те же.</summary>
        private static void ApplyBakedRoom(string path, string modelPath, string script, Material room, Material props, System.Action<Transform> addLive, Material sway = null, Material sky = null, System.Func<string, Material> byMeshName = null)
        {
            if (IsLocked(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            if (AssetImporter.GetAtPath(modelPath) is ModelImporter model)
            {
                model.materialImportMode = ModelImporterMaterialImportMode.None;
                model.importNormals = ModelImporterNormals.None;
                model.importTangents = ModelImporterTangents.None;
                model.importAnimation = false;
                model.importBlendShapes = false;
                model.importCameras = false;
                model.importLights = false;
                model.isReadable = false;
                model.meshCompression = ModelImporterMeshCompression.Low;
                model.SaveAndReimport();
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError("[Zonk] No model " + modelPath + ": run Tools/Blender/" + script + " -- --bake");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (var i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
                instance.name = "Room";
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var named = byMeshName != null ? byMeshName(renderer.name) : null;
                    renderer.sharedMaterial = named != null ? named
                        : renderer.name.Contains("Sway") ? (sway != null ? sway : props)
                        : renderer.name.Contains("SkyBack") && sky != null ? sky
                        : renderer.name.Contains("Props") ? props : room;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                addLive(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material BakedMaterial(string name, string texture, int size)
        {
            var texturePath = RoomTextures + "/" + texture + ".png";
            // Камера смотрит под углом, Unity берёт уменьшенный мип и картинка мылится. Анизотропии нет (все телефоны): резкость дают сдвиг мипов и мягкое сжатие.
            ConfigureTexture(texturePath, size, BakedTextureQuality, BakedTextureAniso);
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer && !Mathf.Approximately(importer.mipMapBias, BakedTextureMipBias))
            {
                importer.mipMapBias = BakedTextureMipBias;
                importer.SaveAndReimport();
            }

            var path = RoomTextures + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Zonk/Baked")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            // Прежние материалы были на URP Unlit: BakedTint профиля работает только на Zonk/Baked.
            material.shader = Shader.Find("Zonk/Baked");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SmokeWispMaterial()
        {
            var path = RoomTextures + "/M_SmokeWisp.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Zonk/SmokeWisp")) { name = "M_SmokeWisp" };
            material.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeFolder + "/Shadow_Noise.png"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject SpriteObject(string name, Transform parent, Vector3 position, float scale, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>Огонь в очаге: те же кадры, что огонь в интерфейсе.</summary>
        private static void AddFlames(Transform root, float fireX, float fireZ)
        {
            var frames = FlameSprites();
            var flames = new[]
            {
                (x: -0.42f, h: 0.75f, offset: 0),
                (x: 0.4f, h: 0.82f, offset: 5),
                (x: 0f, h: 1.15f, offset: 3),
            };
            foreach (var flame in flames)
            {
                // Кадр огня 1.2 единицы в высоту, центр посередине.
                var position = new Vector3(fireX + flame.x, RoomFloor + 0.28f + flame.h * 0.5f, fireZ);
                var go = SpriteObject("Fire", root, position, flame.h / 1.2f, frames[0]);
                go.AddComponent<WorldFlipbook>().EditorSetup(go.GetComponent<SpriteRenderer>(), frames, FlameFps, flame.offset);
            }
        }

        private static void AddCandle(Transform root, Vector3 position)
        {
            var frames = FlameSprites();
            var go = SpriteObject("CandleFlame", root, position, 0.09f, frames[0]);
            go.AddComponent<WorldFlipbook>().EditorSetup(go.GetComponent<SpriteRenderer>(), frames, FlameFps, (int)(position.x * 3f) & 7);
        }

        /// <summary>Дымок над огнём: одна плоскость с шумом.</summary>
        private static void AddSmoke(Transform root, Material material, float fireX, float fireZ)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FireSmoke";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = new Vector3(fireX, RoomFloor + 1.1f, fireZ + 0.02f);
            quad.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Живой отсвет камина на полу.</summary>
        private static void AddFloorGlow(Transform root, float glowX, float glowZ)
        {
            var sprite = SpriteAt(Root + "/Art/Textures/Particle_SoftDot.png");
            if (sprite == null)
                return;

            var go = SpriteObject("FireGlow", root, new Vector3(glowX, RoomFloor + 0.03f, glowZ), 1f, sprite);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var size = sprite.bounds.size;
            go.transform.localScale = new Vector3(5f / Mathf.Max(size.x, 0.01f), 4f / Mathf.Max(size.y, 0.01f), 1f);
            go.AddComponent<WorldGlowPulse>().EditorSetup(go.GetComponent<SpriteRenderer>(), new Color(1f, 0.55f, 0.18f, 0.22f), 0.55f, 2.3f);
        }

        /// <summary>Свет на столе, костях и руках под вечернюю комнату: тёплый и от лампы, а не дневное солнце.</summary>
        private static void TuneHomeLighting()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/Lighting_Home.asset");
            if (profile == null)
                return;

            profile.SunColor = new Color(1f, 0.84f, 0.66f);
            profile.SunIntensity = 0.55f;
            profile.SunRotation = new Vector3(62f, -25f, 0f);
            profile.AmbientSky = new Color(0.42f, 0.36f, 0.33f);
            profile.AmbientEquator = new Color(0.36f, 0.28f, 0.22f);
            profile.AmbientGround = new Color(0.2f, 0.14f, 0.1f);
            profile.Background = new Color(0.05f, 0.04f, 0.04f);
            profile.LampIntensity = 1f;
            profile.TableLight = 1.1f;
            profile.TableLightColor = new Color(1f, 0.84f, 0.62f);
            EditorUtility.SetDirty(profile);
            SetBakedTint("Lighting_HomeMidnight", new Color(0.5f, 0.56f, 0.82f));
        }

        /// <summary>Ночной вариант локации: та же запечённая комната темнее и холоднее. Ручную правку оттенка не затираем.</summary>
        private static void SetBakedTint(string profileName, Color tint)
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/" + profileName + ".asset");
            if (profile == null || profile.BakedTint != Color.white)
                return;

            profile.BakedTint = tint;
            EditorUtility.SetDirty(profile);
        }
    }
}
