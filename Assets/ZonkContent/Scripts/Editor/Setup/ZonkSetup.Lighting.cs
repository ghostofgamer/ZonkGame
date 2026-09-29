using System.Collections.Generic;
using System.IO;
using Base.Services.Quality;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Освещение и атмосфера локаций: профили (LightingProfileConfig), дождь, узор-«cookie» решётки,
    /// новая локация «Подвал» и лампа в решётке. Всё с расчётом на телефоны и браузер: без постобработки,
    /// тени только от солнца и с Medium, у ламп и огней теней нет, решётка — cookie, а не настоящие тени.
    /// Уже созданные ассеты не перезаписываются (профили, префабы, картинки можно править руками).
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string LightingFolder = ConfigsFolder + "/Lighting";

        private static void BuildLighting(ContentSet set, ArtSet art, CurrencyConfig coins)
        {
            EnsureFolder(LightingFolder);

            // Локация задаёт освещение сцены.
            var envSlot = SlotAt("environment");
            if (envSlot != null && !envSlot.DrivesLighting)
            {
                envSlot.DrivesLighting = true;
                EditorUtility.SetDirty(envSlot);
            }

            // Уже созданные префабы ламп и таверны: источникам света — SceneLight (свечи таверны — только с Medium).
            EnsureSceneLights(Prefabs + "/Lamps/Lamp_Basic.prefab", QualityTier.Low);
            EnsureSceneLights(Prefabs + "/Lamps/Lamp_Lantern.prefab", QualityTier.Low);
            EnsureSceneLights(Prefabs + "/Environments/Env_Tavern.prefab", QualityTier.Medium);

            var rain = RainPrefab();

            // Обычная квартира днём: солнце в окно, лампа почти не нужна.
            var home = Profile("Lighting_Home", p =>
            {
                p.SunColor = new Color(1f, 0.95f, 0.86f);
                p.SunIntensity = 1.0f;
                p.SunRotation = new Vector3(45f, -35f, 0f);
                p.AmbientSky = new Color(0.52f, 0.5f, 0.48f);
                p.AmbientEquator = new Color(0.4f, 0.37f, 0.34f);
                p.AmbientGround = new Color(0.22f, 0.2f, 0.18f);
                p.Background = new Color(0.55f, 0.62f, 0.7f);
                p.LampIntensity = 0.6f;
            });

            // Подвал: солнца нет, всё в темноте, единственный свет — лампа над столом.
            var basement = Profile("Lighting_Basement", p =>
            {
                p.Sun = false;
                p.AmbientSky = new Color(0.05f, 0.05f, 0.06f);
                p.AmbientEquator = new Color(0.04f, 0.035f, 0.03f);
                p.AmbientGround = new Color(0.02f, 0.02f, 0.02f);
                p.Fog = true;
                p.FogColor = new Color(0.02f, 0.02f, 0.025f);
                p.FogDensity = 0.09f;
                p.Background = Color.black;
                p.LampIntensity = 2.2f;
            });

            // Таверна вечером: тёплый приглушённый свет, лампа и свечи.
            var tavern = Profile("Lighting_Tavern", p =>
            {
                p.SunColor = new Color(1f, 0.6f, 0.35f);
                p.SunIntensity = 0.25f;
                p.SunRotation = new Vector3(20f, 60f, 0f);
                p.AmbientSky = new Color(0.25f, 0.18f, 0.12f);
                p.AmbientEquator = new Color(0.2f, 0.14f, 0.1f);
                p.AmbientGround = new Color(0.1f, 0.07f, 0.05f);
                p.Background = new Color(0.08f, 0.05f, 0.03f);
                p.LampIntensity = 1.4f;
            });

            // Пляж: яркое солнце, светлые тени, дымка у горизонта; лампа только для вида.
            var beach = Profile("Lighting_Beach", p =>
            {
                p.SunColor = new Color(1f, 0.97f, 0.9f);
                p.SunIntensity = 1.3f;
                p.SunRotation = new Vector3(55f, -30f, 0f);
                p.AmbientSky = new Color(0.6f, 0.7f, 0.85f);
                p.AmbientEquator = new Color(0.55f, 0.55f, 0.5f);
                p.AmbientGround = new Color(0.45f, 0.4f, 0.3f);
                p.Fog = true;
                p.FogColor = new Color(0.75f, 0.85f, 0.95f);
                p.FogDensity = 0.004f;
                p.Background = new Color(0.55f, 0.75f, 0.95f);
                p.LampIntensity = 0.15f;
            });

            // Корабль ночью в шторм: холодный лунный свет, туман, дождь и молнии.
            var ship = Profile("Lighting_Ship", p =>
            {
                p.SunColor = new Color(0.55f, 0.65f, 0.9f);
                p.SunIntensity = 0.25f;
                p.SunRotation = new Vector3(35f, 160f, 0f);
                p.AmbientSky = new Color(0.12f, 0.14f, 0.2f);
                p.AmbientEquator = new Color(0.08f, 0.09f, 0.12f);
                p.AmbientGround = new Color(0.04f, 0.04f, 0.05f);
                p.Fog = true;
                p.FogColor = new Color(0.08f, 0.09f, 0.12f);
                p.FogDensity = 0.035f;
                p.Background = new Color(0.05f, 0.06f, 0.09f);
                p.LampIntensity = 1.3f;
                p.Effects = new List<AtmosphereEffect>
                {
                    new RainEffect { Prefab = rain },
                    new LightningEffect(),
                };
            });

            if (set.Config.DefaultLighting == null)
            {
                set.Config.DefaultLighting = home;
                EditorUtility.SetDirty(set.Config);
            }

            SetLighting(ItemAt("env_home"), home);
            SetLighting(ItemAt("env_tavern"), tavern);
            SetLighting(ItemAt("env_beach"), beach);
            SetLighting(ItemAt("env_ship"), ship);

            // Подвал — новая локация, и лампа в решётке: узор решётки ложится на стол (cookie у прожектора).
            var basementPrefab = BasementPrefab(art);
            var basementItem = Item("env_basement", envSlot, 4, new PrefabPayload { Prefab = basementPrefab, Lighting = basement },
                Coins(coins, 5000), new RewardedAdPriceOption { AdsRequired = 6 }, new PurchasePriceOption { ProductId = "env_basement" });
            SetLighting(basementItem, basement);
            Item("lamp_cage", SlotAt("lamp"), 4, new PrefabPayload { Prefab = CageLampPrefab(art) },
                Coins(coins, 2000), new PurchasePriceOption { ProductId = "lamp_cage" });
        }

        /// <summary>Чердак: деревянная комната со скатом крыши и круглым окном.</summary>
        private static GameObject AtticPrefab()
        {
            var wood = Mat(new ArtSet(), "Floor_Wood", new Color(0.4f, 0.27f, 0.16f), null, 0.2f);
            var plank = Mat(new ArtSet(), "Attic_Plank", new Color(0.55f, 0.4f, 0.26f), null, 0.2f);
            var window = Mat(new ArtSet(), "Window", new Color(0.5f, 0.7f, 0.9f), null, 0.9f, 0f, new Color(0.6f, 0.8f, 1.1f));
            var barrel = Mat(new ArtSet(), "Barrel", new Color(0.45f, 0.28f, 0.14f), null, 0.3f);
            return Prefab(Prefabs + "/Environments/Env_Attic.prefab", () =>
            {
                var root = Room("Env_Attic", wood, plank);
                Prim(PrimitiveType.Cube, "RoofLeft", root.transform, new Vector3(-4.5f, 6.2f, 0f), new Vector3(11f, 0.2f, 20f), plank, new Vector3(0f, 0f, 35f));
                Prim(PrimitiveType.Cube, "RoofRight", root.transform, new Vector3(4.5f, 6.2f, 0f), new Vector3(11f, 0.2f, 20f), plank, new Vector3(0f, 0f, -35f));
                Prim(PrimitiveType.Cylinder, "RoundWindow", root.transform, new Vector3(0f, 3.4f, 7.88f), new Vector3(1.6f, 0.02f, 1.6f), window, new Vector3(90f, 0f, 0f));
                Prim(PrimitiveType.Cube, "Trunk", root.transform, new Vector3(-5.5f, FloorY + 0.45f, 6f), new Vector3(1.8f, 0.9f, 1f), barrel);
                Prim(PrimitiveType.Cube, "Beam", root.transform, new Vector3(0f, 5f, 0f), new Vector3(0.3f, 0.3f, 20f), barrel);
                return root;
            });
        }

        /// <summary>Сад: трава, живая изгородь, деревья, небо.</summary>
        private static GameObject GardenPrefab()
        {
            var grass = Mat(new ArtSet(), "Grass", new Color(0.3f, 0.55f, 0.25f), null, 0.1f);
            var hedge = Mat(new ArtSet(), "Hedge", new Color(0.18f, 0.4f, 0.18f), null, 0.1f);
            var trunk = Mat(new ArtSet(), "Trunk", new Color(0.4f, 0.28f, 0.18f), null, 0.2f);
            var crown = Mat(new ArtSet(), "Tree_Crown", new Color(0.25f, 0.5f, 0.22f), null, 0.1f);
            var sky = Mat(new ArtSet(), "Sky", new Color(0.55f, 0.75f, 0.95f), null, 0f, 0f, new Color(0.35f, 0.5f, 0.7f));
            return Prefab(Prefabs + "/Environments/Env_Garden.prefab", () =>
            {
                var root = new GameObject("Env_Garden");
                Prim(PrimitiveType.Plane, "Grass", root.transform, new Vector3(0f, FloorY, 0f), new Vector3(4f, 1f, 4f), grass);
                Prim(PrimitiveType.Quad, "Sky", root.transform, new Vector3(0f, 8f, 40f), new Vector3(140f, 45f, 1f), sky);
                Prim(PrimitiveType.Cube, "Hedge", root.transform, new Vector3(0f, FloorY + 0.7f, 12f), new Vector3(24f, 1.4f, 1f), hedge);
                foreach (var position in new[] { new Vector3(-8f, 0f, 9f), new Vector3(9f, 0f, 10f), new Vector3(-11f, 0f, 2f) })
                {
                    Prim(PrimitiveType.Cylinder, "Trunk", root.transform, position + new Vector3(0f, FloorY + 1.5f, 0f), new Vector3(0.4f, 1.5f, 0.4f), trunk);
                    Prim(PrimitiveType.Sphere, "Crown", root.transform, position + new Vector3(0f, FloorY + 3.8f, 0f), new Vector3(3.2f, 2.8f, 3.2f), crown);
                }

                return root;
            });
        }

        /// <summary>Чердак вечером: тёплый низкий луч из окна, приглушённо, лампа важна.</summary>
        private static LightingProfileConfig AtticLighting()
        {
            return Profile("Lighting_Attic", p =>
            {
                p.SunColor = new Color(1f, 0.72f, 0.45f);
                p.SunIntensity = 0.55f;
                p.SunRotation = new Vector3(18f, 170f, 0f);
                p.AmbientSky = new Color(0.28f, 0.22f, 0.17f);
                p.AmbientEquator = new Color(0.22f, 0.17f, 0.12f);
                p.AmbientGround = new Color(0.1f, 0.08f, 0.06f);
                p.Background = new Color(0.1f, 0.07f, 0.05f);
                p.LampIntensity = 1.1f;
            });
        }

        /// <summary>Сад днём: мягкое солнце, зелёные отсветы снизу, лампа для вида.</summary>
        private static LightingProfileConfig GardenLighting()
        {
            return Profile("Lighting_Garden", p =>
            {
                p.SunColor = new Color(1f, 0.96f, 0.88f);
                p.SunIntensity = 1.15f;
                p.SunRotation = new Vector3(50f, 25f, 0f);
                p.AmbientSky = new Color(0.55f, 0.65f, 0.8f);
                p.AmbientEquator = new Color(0.45f, 0.5f, 0.42f);
                p.AmbientGround = new Color(0.25f, 0.35f, 0.2f);
                p.Fog = true;
                p.FogColor = new Color(0.7f, 0.8f, 0.9f);
                p.FogDensity = 0.006f;
                p.Background = new Color(0.55f, 0.75f, 0.95f);
                p.LampIntensity = 0.2f;
            });
        }

        private static LightingProfileConfig Profile(string name, System.Action<LightingProfileConfig> init)
        {
            return Asset(LightingFolder + "/" + name + ".asset", init);
        }

        private static void SetLighting(CosmeticItemConfig item, LightingProfileConfig profile)
        {
            if (item == null || !(item.Payload is PrefabPayload prefab) || prefab.Lighting != null)
                return;

            prefab.Lighting = profile;
            EditorUtility.SetDirty(item);
        }

        /// <summary>Добавить SceneLight источникам света префаба, у которых его нет.</summary>
        private static void EnsureSceneLights(string path, QualityTier minTier)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            var changed = false;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional || light.GetComponent<SceneLight>() != null)
                    continue;

                var sceneLight = light.gameObject.AddComponent<SceneLight>();
                var serialized = new SerializedObject(sceneLight);
                serialized.FindProperty("_minTier").enumValueIndex = (int)minTier;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>Подвал: тёмная каменная комната, ящики и бочки по углам. Своих источников света нет — только лампа.</summary>
        private static GameObject BasementPrefab(ArtSet art)
        {
            return Prefab(Prefabs + "/Environments/Env_Basement.prefab", () =>
            {
                var root = Room("Env_Basement", art["Stone"], art["Stone"]);
                Prim(PrimitiveType.Cube, "Ceiling", root.transform, new Vector3(0f, 6.4f, 0f), new Vector3(20f, 0.2f, 20f), art["Stone"]);
                Prim(PrimitiveType.Cube, "Crate", root.transform, new Vector3(-5.5f, FloorY + 0.6f, 6f), new Vector3(1.4f, 1.2f, 1.4f), art["Barrel"]);
                Prim(PrimitiveType.Cube, "Crate", root.transform, new Vector3(-4.3f, FloorY + 0.45f, 6.4f), new Vector3(1f, 0.9f, 1f), art["Barrel"]);
                Prim(PrimitiveType.Cylinder, "Barrel", root.transform, new Vector3(5.5f, FloorY + 0.6f, 6f), new Vector3(1f, 0.6f, 1f), art["Barrel"]);
                Prim(PrimitiveType.Cube, "Pipe", root.transform, new Vector3(0f, 5.6f, 7.6f), new Vector3(18f, 0.25f, 0.25f), art["Metal"]);
                return root;
            });
        }

        /// <summary>
        /// Лампа в решётке: плафон, прутья вокруг лампочки и прожектор вниз с cookie решётки — на столе видна тень
        /// прутьев, но без настоящих теней (они дорогие). Мягкое свечение вокруг — точечный свет без теней.
        /// </summary>
        private static GameObject CageLampPrefab(ArtSet art)
        {
            return Prefab(Prefabs + "/Lamps/Lamp_Cage.prefab", () =>
            {
                var root = new GameObject("Lamp_Cage");
                Prim(PrimitiveType.Cylinder, "Cord", root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.03f, 1f, 0.03f), art["Black"]);
                Prim(PrimitiveType.Cylinder, "Cap", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.05f, 0.45f), art["Metal"]);
                Prim(PrimitiveType.Sphere, "Bulb", root.transform, new Vector3(0f, -0.2f, 0f), Vector3.one * 0.26f, art["Bulb"]);
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Mathf.PI * 2f / 8f;
                    var position = new Vector3(Mathf.Cos(angle) * 0.2f, -0.2f, Mathf.Sin(angle) * 0.2f);
                    Prim(PrimitiveType.Cube, "Bar", root.transform, position, new Vector3(0.025f, 0.46f, 0.025f), art["Metal"]);
                }

                Prim(PrimitiveType.Cylinder, "Ring", root.transform, new Vector3(0f, -0.43f, 0f), new Vector3(0.44f, 0.01f, 0.44f), art["Metal"]);

                // Прожектор вниз с узором решётки — главный свет на стол.
                var spot = AddLight(root.transform, new Vector3(0f, -0.2f, 0f), new Color(1f, 0.86f, 0.62f), 7f, 9f);
                spot.type = LightType.Spot;
                spot.spotAngle = 95f;
                spot.innerSpotAngle = 60f;
                spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                spot.cookie = CageCookie();

                // Слабый общий свет вокруг лампы, чтобы комната не была совсем чёрной (без теней, дёшево).
                AddLight(root.transform, new Vector3(0f, -0.3f, 0f), new Color(1f, 0.8f, 0.55f), 0.6f, 6f);
                return root;
            });
        }

        /// <summary>Узор решётки для прожектора: светлое пятно с тёмными прутьями, края мягкие.</summary>
        private static Texture2D CageCookie()
        {
            var path = Textures + "/Cookie_Cage.png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var u = x / (float)size - 0.5f;
                    var v = y / (float)size - 0.5f;
                    var radius = Mathf.Sqrt(u * u + v * v) * 2f;
                    var spot = 1f - Mathf.SmoothStep(0.7f, 1f, radius);

                    // Прутья идут лучами от центра (как у решётки лампы) плюс одно кольцо.
                    var angle = Mathf.Atan2(v, u);
                    var bars = Mathf.Abs(Mathf.Sin(angle * 4f));
                    var bar = Mathf.SmoothStep(0.06f, 0.12f, bars);
                    var ring = Mathf.SmoothStep(0.02f, 0.04f, Mathf.Abs(radius - 0.55f));
                    var value = spot * Mathf.Lerp(0.15f, 1f, bar * ring);
                    pixels[y * size + x] = new Color(value, value, value, value);
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Cookie;
                    importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Дождь: одна система частиц без столкновений, капли — вытянутые полупрозрачные штрихи. Число капель
        /// RainEffect выставляет по уровню качества (150 / 350 / 700).
        /// </summary>
        private static GameObject RainPrefab()
        {
            return Prefab(Prefabs + "/Effects/Rain.prefab", () =>
            {
                var root = new GameObject("Rain");
                var system = root.AddComponent<ParticleSystem>();
                root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var main = system.main;
                main.loop = true;
                main.duration = 1f;
                main.startLifetime = 0.9f;
                main.startSpeed = 11f;
                main.startSize = 0.025f;
                main.maxParticles = 700;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.35f);

                var emission = system.emission;
                emission.rateOverTime = 780f;

                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(14f, 10f, 0.1f);

                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 7f;
                renderer.velocityScale = 0.02f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = RainMaterial();
                return root;
            });
        }

        private static Material RainMaterial()
        {
            var path = Materials + "/M_Rain.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_Rain" };
            // Прозрачный режим URP-частиц: смешивание по альфе, без записи глубины.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
