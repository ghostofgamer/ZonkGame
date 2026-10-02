using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>Материалы и префабы, на которые ссылаются контент и сцены.</summary>
    public sealed class ArtSet
    {
        public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        public Mesh DieMesh;
        public GameObject CupLeather;
        public GameObject CupWood;

        public GameObject Table;
        public GameObject Felt;
        public GameObject LampBasic;
        public GameObject LampLantern;
        public GameObject EnvHome;
        public GameObject EnvTavern;
        public GameObject EnvBeach;
        public GameObject EnvShip;
        public GameObject HatTop;
        public GameObject HatPirate;
        public GameObject Hood;

        public Material this[string name] => Materials[name];
    }

    public static partial class ZonkSetup
    {
        private const string LitShader = "Universal Render Pipeline/Lit";
        private const float DieSize = 0.3f;
        private const float CupHeight = 0.9f;
        private const int CupTextureSize = 1024;
        private const int TextureCrunchQuality = 50;

        /// <summary>Стаканы скрипта zonk_cups.py: имя модели, гладкость и металличность материала.</summary>
        private static readonly (string Name, float Smoothness, float Metallic)[] GeneratedCups =
        {
            ("Cup_Goblet", 0.7f, 0.9f),
            ("Cup_Bone", 0.5f, 0f),
            ("Cup_Clay", 0.65f, 0f),
            ("Cup_Barrel", 0.3f, 0f),
            ("Cup_Coconut", 0.15f, 0f),
            ("Cup_Copper", 0.6f, 0.85f),
            ("Cup_Stone", 0.1f, 0f),
        };

        public static ArtSet BuildArt()
        {
            ConfigureModel(Models + "/Die.fbx", true);
            ConfigureModel(Models + "/Cup_Leather.fbx", false);
            ConfigureModel(Models + "/Cup_Wood.fbx", false);

            var art = new ArtSet();
            var dieTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Die_Classic.png");
            var leather = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Cup_Leather.png");
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + "/Cup_Wood.png");

            // Кости: текстура белая с тёмными точками, цвет скина задаёт _BaseColor. Эмиссия включена для меток особых костей.
            Mat(art, "Die_Ivory", new Color(1f, 0.97f, 0.9f), dieTexture, 0.55f, 0f, Color.black);
            Mat(art, "Die_Sapphire", new Color(0.55f, 0.7f, 1f), dieTexture, 0.7f, 0f, Color.black);
            Mat(art, "Die_Ruby", new Color(1f, 0.5f, 0.5f), dieTexture, 0.7f, 0f, Color.black);
            Mat(art, "Die_Jade", new Color(0.55f, 0.9f, 0.6f), dieTexture, 0.6f, 0f, Color.black);
            Mat(art, "Die_Obsidian", new Color(0.55f, 0.52f, 0.6f), dieTexture, 0.85f, 0.2f, Color.black);
            Mat(art, "Die_Gold", new Color(1f, 0.82f, 0.4f), dieTexture, 0.8f, 0.7f, Color.black);

            Mat(art, "Cup_Leather", Color.white, leather, 0.35f);
            Mat(art, "Cup_Wood", Color.white, wood, 0.3f);
            Mat(art, "Cup_Gold", new Color(1f, 0.8f, 0.35f), leather, 0.85f, 0.9f);

            Mat(art, "Table_Oak", new Color(0.55f, 0.36f, 0.2f), null, 0.35f);
            Mat(art, "Table_Dark", new Color(0.22f, 0.13f, 0.08f), null, 0.45f);
            Mat(art, "Table_Marble", new Color(0.9f, 0.9f, 0.88f), null, 0.8f);
            Mat(art, "Felt_Green", new Color(0.12f, 0.42f, 0.22f), null, 0.05f);
            Mat(art, "Felt_Red", new Color(0.5f, 0.1f, 0.12f), null, 0.05f);
            Mat(art, "Felt_Blue", new Color(0.12f, 0.22f, 0.5f), null, 0.05f);
            Mat(art, "Frame", new Color(0.35f, 0.2f, 0.1f), null, 0.4f);
            Mat(art, "Metal", new Color(0.3f, 0.3f, 0.3f), null, 0.6f, 0.8f);
            Mat(art, "Skin", new Color(0.85f, 0.65f, 0.5f), null, 0.3f);
            Mat(art, "Silhouette", new Color(0.18f, 0.17f, 0.2f), null, 0.2f);
            Mat(art, "Eyes", new Color(1f, 0.85f, 0.4f), null, 0.5f, 0f, new Color(2.5f, 1.8f, 0.6f));
            Mat(art, "Ring", new Color(1f, 0.8f, 0.3f), null, 0.5f, 0f, new Color(1.5f, 1.1f, 0.3f));
            Mat(art, "Lamp", new Color(0.25f, 0.3f, 0.2f), null, 0.4f, 0.3f);
            Mat(art, "Bulb", new Color(1f, 0.9f, 0.7f), null, 0.5f, 0f, new Color(3f, 2.5f, 1.6f));
            Mat(art, "Floor_Wood", new Color(0.4f, 0.27f, 0.16f), null, 0.2f);
            Mat(art, "Wall_Home", new Color(0.72f, 0.62f, 0.5f), null, 0.1f);
            Mat(art, "Window", new Color(0.5f, 0.7f, 0.9f), null, 0.9f, 0f, new Color(0.6f, 0.8f, 1.1f));
            Mat(art, "Rug", new Color(0.55f, 0.15f, 0.12f), null, 0.05f);
            Mat(art, "Stone", new Color(0.35f, 0.33f, 0.32f), null, 0.15f);
            Mat(art, "Barrel", new Color(0.45f, 0.28f, 0.14f), null, 0.3f);
            Mat(art, "Candle", new Color(1f, 0.85f, 0.5f), null, 0.3f, 0f, new Color(2f, 1.3f, 0.4f));
            Mat(art, "Sand", new Color(0.93f, 0.84f, 0.62f), null, 0.1f);
            Mat(art, "Sea", new Color(0.1f, 0.45f, 0.65f), null, 0.9f);
            Mat(art, "Sky", new Color(0.55f, 0.8f, 1f), null, 0f, 0f, new Color(0.45f, 0.7f, 1f));
            Mat(art, "Palm", new Color(0.2f, 0.55f, 0.2f), null, 0.2f);
            Mat(art, "Trunk", new Color(0.5f, 0.35f, 0.2f), null, 0.2f);
            Mat(art, "Sun", new Color(1f, 0.95f, 0.7f), null, 0f, 0f, new Color(4f, 3.5f, 2f));
            Mat(art, "Deck", new Color(0.5f, 0.33f, 0.18f), null, 0.25f);
            Mat(art, "Sail", new Color(0.95f, 0.92f, 0.85f), null, 0.1f);
            Mat(art, "Black", new Color(0.08f, 0.08f, 0.08f), null, 0.3f);
            Mat(art, "Red", new Color(0.7f, 0.12f, 0.1f), null, 0.3f);

            art.DieMesh = MeshOf(Models + "/Die.fbx");
            BuildDiePrefab(art);
            art.CupLeather = BuildCupPrefab("Cup_Leather", Models + "/Cup_Leather.fbx", art["Cup_Leather"]);
            art.CupWood = BuildCupPrefab("Cup_Wood", Models + "/Cup_Wood.fbx", art["Cup_Wood"]);
            foreach (var (name, smoothness, metallic) in GeneratedCups)
            {
                var path = Models + "/Cups/" + name + ".fbx";
                if (!(AssetImporter.GetAtPath(path) is ModelImporter))
                    continue;

                ConfigureModel(path, false);
                var texturePath = Textures + "/Cups/" + name + ".png";
                ConfigureTexture(texturePath, CupTextureSize);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var material = Mat(art, name, Color.white, texture, smoothness, metallic);
                BuildCupPrefab(name, path, material);
            }

            BuildPlaceholders(art);

            var errors = new List<string>();
            if (art.DieMesh != null)
                ContentValidator.ValidateDieMesh("Die.fbx", art.DieMesh, errors);
            foreach (var error in errors)
                Debug.LogError("[Zonk] " + error);

            return art;
        }

        private static void ConfigureModel(string path, bool readable)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            {
                Debug.LogError("[Zonk] Model not found: " + path + ". Run Tools/Blender/zonk_models.py");
                return;
            }

            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && importer.isReadable == readable &&
                !importer.importAnimation)
                return;

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = readable;
            importer.importAnimation = false;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Текстура для телефонов и браузера: не больше maxSize, сжатая, с мип-уровнями и Crunch — файл в сборке
        /// в несколько раз меньше (важно для загрузки в браузере), в памяти видеокарты как обычное сжатие.
        /// </summary>
        private static void ConfigureTexture(string path, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;

            if (importer.maxTextureSize == maxSize && importer.crunchedCompression && importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Compressed)
                return;

            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = TextureCrunchQuality;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private static Mesh MeshOf(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
        }

        /// <summary>Материал URP Lit (создаётся, если его нет); art — куда записать его по имени (null — никуда).</summary>
        private static Material Mat(ArtSet art, string name, Color color, Texture2D texture = null, float smoothness = 0.3f,
            float metallic = 0f, Color? emission = null)
        {
            var path = Materials + "/M_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(LitShader)) { name = "M_" + name };
                material.SetColor("_BaseColor", color);
                if (texture != null)
                    material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                if (emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                AssetDatabase.CreateAsset(material, path);
            }

            if (art != null)
                art.Materials[name] = material;
            return material;
        }

        /// <summary>Кость: корень с коллайдером для клика и DieView, дочерний Visual с мешем из Blender.</summary>
        private static GameObject BuildDiePrefab(ArtSet art)
        {
            return Prefab(Prefabs + "/Dice/Die.prefab", () =>
            {
                var root = new GameObject("Die");
                root.AddComponent<BoxCollider>().size = Vector3.one * DieSize;

                var visual = Empty("Visual", root.transform);
                var filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = art.DieMesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = art["Die_Ivory"];
                if (art.DieMesh != null && art.DieMesh.bounds.size.x > 0f)
                    visual.transform.localScale = Vector3.one * (DieSize / art.DieMesh.bounds.size.x);

                var ring = Prim(PrimitiveType.Cylinder, "SelectionRing", root.transform, new Vector3(0f, -DieSize * 0.48f, 0f),
                    new Vector3(DieSize * 1.45f, 0.004f, DieSize * 1.45f), art["Ring"]);
                ring.SetActive(false);

                root.AddComponent<DieView>().EditorSetup(visual.transform, filter, renderer, ring);
                return root;
            });
        }

        /// <summary>
        /// Стакан: меш из Blender и дочерний "Mouth" на высоте горла. Если в модели есть маркер "Inside"
        /// (внутреннее дно и свободный радиус), он переносится в префаб.
        /// </summary>
        private static GameObject BuildCupPrefab(string name, string modelPath, Material material)
        {
            return Prefab(Prefabs + "/Cups/" + name + ".prefab", () =>
            {
                var root = new GameObject(name);
                var mesh = MeshOf(modelPath);
                var model = Empty("Model", root.transform);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterial = material;

                var height = mesh != null ? mesh.bounds.max.y : CupHeight;
                if (height > 0.01f && Mathf.Abs(height - CupHeight) > 0.05f)
                {
                    model.transform.localScale = Vector3.one * (CupHeight / height);
                    height = CupHeight;
                }

                Empty(CupView.MouthName, root.transform, new Vector3(0f, height, 0f));

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var inside = source != null ? source.transform.Find(CupView.InsideName) : null;
                if (inside != null)
                    Empty(CupView.InsideName, root.transform, Vector3.Scale(inside.localPosition, model.transform.localScale));

                return root;
            });
        }
    }
}
