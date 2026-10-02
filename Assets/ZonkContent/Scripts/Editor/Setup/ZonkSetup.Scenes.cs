using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;
using Zonk.Boot;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Table;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Сцены игры. Bootstrap: логотип и полоса загрузки. Table: стол со всеми якорями косметики, ракурсами камеры,
    /// местами игроков, соперником-заглушкой и UI. Раскладка: верх стола y = 0, игрок 1 на юге (−Z), камера с юга.
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string BootstrapScene = ScenesFolder + "/" + SceneNames.Bootstrap + ".unity";
        public const string TableScene = ScenesFolder + "/" + SceneNames.Table + ".unity";

        private const int IgnoreRaycastLayer = 2;

        public static void BuildScenes(ContentSet content)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Заблокированная сцена (ZonkLocked) не пересоздаётся: в ней ручные правки.
            if (!SkipIfLocked(BootstrapScene))
                BuildBootstrapScene(content);

            if (IsLocked(TableScene))
                Debug.Log($"[Zonk] {TableScene} is locked ({LockLabel}): kept as is. New scene objects from the generator are not added");
            else
                BuildTableScene(content);
        }

        private static void BuildBootstrapScene(ContentSet content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.07f, 0.05f);

            // Экран загрузки из префаба: полоса и подсказки. Он переживёт загрузку стола и закроется из меню.
            if (content.LoadingScreen != null)
                PrefabUtility.InstantiatePrefab(content.LoadingScreen);

            var context = new GameObject("SceneContext").AddComponent<SceneContext>();
            context.gameObject.AddComponent<BootstrapEntry>();

            EditorSceneManager.SaveScene(scene, BootstrapScene);
        }

        private static void BuildTableScene(ContentSet content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.31f, 0.3f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var anchors = new List<CosmeticAnchor>
            {
                Anchor("Environment", null, Vector3.zero, "environment"),
                Anchor("Table", null, Vector3.zero, "table"),
                Anchor("Lamp", null, new Vector3(0f, 3.6f, 0f), "lamp"),
                DecorAnchor(),
            };

            anchors.AddRange(DecorSpotAnchors());

            var tray = BuildTray(out var feltAnchor);
            anchors.Add(feltAnchor);

            var south = BuildSeat("Seat_South", 1f, "cup", out var southCup);
            var north = BuildSeat("Seat_North", -1f, "cup_north", out var northCup);
            anchors.Add(southCup);
            anchors.Add(northCup);

            var opponent = BuildOpponent();
            var dice = BuildDice();
            var rig = BuildCameraRig();

            var stage = new GameObject("Stage").AddComponent<CosmeticStage>();
            stage.EditorSetup(anchors, dice);

            var soundGo = new GameObject("Sound");
            var source = soundGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var sound = soundGo.AddComponent<SoundPlayer>();
            sound.EditorSetup(source);

            // Свет и атмосфера по профилю локации. Постобработка (Volume) выключена, пока профиль её не включит
            // на подходящем уровне качества: для телефонов и браузера она дорогая.
            var volume = new GameObject("PostProcess").AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.enabled = false;
            var cameraData = rig.Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = rig.Camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            // Пятно света на стол: прожектор из точки лампы вниз; сила и цвет — профиль локации (TableLight), иначе выключен.
            var tableLight = new GameObject("TableLight").AddComponent<Light>();
            tableLight.type = LightType.Spot;
            tableLight.transform.position = new Vector3(0f, 3.3f, 0f);
            tableLight.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            tableLight.range = 8f;
            tableLight.spotAngle = 95f;
            tableLight.shadows = LightShadows.None;
            tableLight.intensity = 0f;
            tableLight.enabled = false;
            new GameObject("Lighting").AddComponent<LightingDirector>().EditorSetup(sun, rig.Camera, volume, stage, sound, tableLight);

            var canvas = AddScaledCanvas(new GameObject("UI"));
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var table = new GameObject("TableView").AddComponent<TableView>();
            // Корень UI — безопасная зона внутри канваса: вырезы и системные панели телефона не перекрывают кнопки.
            var safeArea = UiRect("SafeArea", canvas.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<Zonk.UI.UiSafeArea>();
            table.EditorSetup(rig, tray, dice, south, north, opponent, stage, sound, safeArea);

            var contextGo = new GameObject("SceneContext");
            var installer = contextGo.AddComponent<TableInstaller>();
            installer.EditorSetup(table);
            var context = contextGo.AddComponent<SceneContext>();
            context.Installers = new MonoInstaller[] { installer };

            // Стол сразу в экипировке по умолчанию: сцена выглядит собранной и в редакторе.
            foreach (var slot in content.Slots)
            {
                if (slot.DefaultItem != null && slot.Applier is AnchorPrefabApplier)
                {
                    foreach (var anchor in anchors.Where(a => a.SlotId == slot.Id))
                        PreviewInEditor(anchor, slot.DefaultItem);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TableScene);
        }

        /// <summary>
        /// Предпросмотр в сцене редактора: экземпляр префаба в якоре. В игре якорь пересоздаёт его сам,
        /// поэтому объект помечен как несохраняемый, чтобы не дублировать экипировку.
        /// </summary>
        private static void PreviewInEditor(CosmeticAnchor anchor, CosmeticItemConfig item)
        {
            var source = item.Payload is PrefabPayload prefab ? EditorPrefabOf(prefab) : null;
            if (source == null)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, anchor.transform);
            instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.NotEditable;
        }

        /// <summary>Безделушка на переднем левом углу стола, зеркально стакану игрока; повёрнута к камере меню.</summary>
        private static CosmeticAnchor DecorAnchor()
        {
            var anchor = Anchor("Decor", null, new Vector3(-2.25f, 0f, -1.3f), DecorSlotId);
            anchor.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            return anchor;
        }

        /// <summary>
        /// Места безделушек на столе (верх столешницы — y = 0, стол 5.6 × 4): 0 — передний левый угол (основное), 1 — задний
        /// правый угол у соперника, 2 и 3 — середины левого и правого края рядом с лотком (лоток |x| ≤ 1.7). Вне пути
        /// стаканов к лотку (стаканы в (2.15, −1.25) и (−2.15, 1.25)), отложенных костей (z = ±1.45 у центра) и руки (z = −2.3).
        /// </summary>
        private static readonly Vector3[] DecorSpots =
        {
            new Vector3(-2.25f, 0f, -1.3f),
            new Vector3(2.25f, 0f, 1.3f),
            new Vector3(-2.45f, 0f, 0.15f),
            new Vector3(2.45f, 0f, 0.15f),
        };

        private static readonly Vector3 DecorShotOffset = new Vector3(1.65f, 1.65f, -2.6f);
        private static readonly Vector3 DecorLookUp = new Vector3(0f, 0.25f, 0f);

        /// <summary>Якоря дополнительных мест (1..3): открываются талантом, пустое место ничего не показывает.</summary>
        private static CosmeticAnchor[] DecorSpotAnchors()
        {
            var result = new CosmeticAnchor[DecorSpots.Length - 1];
            for (var spot = 1; spot < DecorSpots.Length; spot++)
            {
                var anchor = Anchor("Decor" + (spot + 1), null, DecorSpots[spot], DecorSlotId);
                anchor.EditorSetup(DecorSlotId, spot);
                anchor.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
                result[spot - 1] = anchor;
            }

            return result;
        }

        private static CosmeticAnchor Anchor(string name, Transform parent, Vector3 position, string slotId)
        {
            var go = Empty("Anchor_" + name, parent, position);
            var anchor = go.AddComponent<CosmeticAnchor>();
            anchor.EditorSetup(slotId);
            return anchor;
        }

        private static DiceTrayView BuildTray(out CosmeticAnchor felt)
        {
            var root = new GameObject("Tray");
            var center = Empty("Center", root.transform, new Vector3(0f, 0.02f, 0f));
            var colliders = new List<BoxCollider>
            {
                Wall(root.transform, "Floor", new Vector3(0f, -0.03f, 0f), new Vector3(3.4f, 0.1f, 2.2f)),
                Wall(root.transform, "WallLeft", new Vector3(-1.65f, 0.8f, 0f), new Vector3(0.1f, 1.6f, 2.2f)),
                Wall(root.transform, "WallRight", new Vector3(1.65f, 0.8f, 0f), new Vector3(0.1f, 1.6f, 2.2f)),
                Wall(root.transform, "WallFront", new Vector3(0f, 0.8f, -1.05f), new Vector3(3.4f, 1.6f, 0.1f)),
                Wall(root.transform, "WallBack", new Vector3(0f, 0.8f, 1.05f), new Vector3(3.4f, 1.6f, 0.1f)),
            };

            // Видимая рамка лотка. Высокие стенки выше неё невидимы: кости не вылетают со стола.
            var frame = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/M_Frame.mat");
            Prim(PrimitiveType.Cube, "FrameLeft", root.transform, new Vector3(-1.65f, 0.09f, 0f), new Vector3(0.1f, 0.18f, 2.2f), frame);
            Prim(PrimitiveType.Cube, "FrameRight", root.transform, new Vector3(1.65f, 0.09f, 0f), new Vector3(0.1f, 0.18f, 2.2f), frame);
            Prim(PrimitiveType.Cube, "FrameFront", root.transform, new Vector3(0f, 0.09f, -1.05f), new Vector3(3.4f, 0.18f, 0.1f), frame);
            Prim(PrimitiveType.Cube, "FrameBack", root.transform, new Vector3(0f, 0.09f, 1.05f), new Vector3(3.4f, 0.18f, 0.1f), frame);

            felt = Anchor("Felt", root.transform, Vector3.zero, "tray");

            var tray = root.AddComponent<DiceTrayView>();
            tray.EditorSetup(colliders, center.transform, new Vector2(3.2f, 2f), DieSize);
            return tray;
        }

        private static BoxCollider Wall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = Empty(name, parent, position);
            go.layer = IgnoreRaycastLayer;
            var collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            return collider;
        }

        /// <summary>Место игрока. side = 1 для юга (у камеры), −1 для севера: всё зеркально.</summary>
        private static SeatView BuildSeat(string name, float side, string cupSlot, out CosmeticAnchor cup)
        {
            var root = new GameObject(name);
            var yaw = side > 0 ? 0f : 180f;

            cup = Anchor("Cup", root.transform, new Vector3(2.15f * side, 0f, -1.25f * side), cupSlot);
            var rest = Empty("HandRest", root.transform, new Vector3(2.2f * side, 0.3f, -2.3f * side), new Vector3(0f, yaw - 20f, 0f));
            var kept = Empty("KeptArea", root.transform, new Vector3(0f, 0.02f, -1.45f * side), new Vector3(0f, yaw, 0f));
            var shake = Empty("ShakePoint", root.transform, new Vector3(0.8f * side, 1.0f, -0.55f * side), new Vector3(0f, yaw - 30f, 0f));
            var bubble = Empty("BubbleAnchor", root.transform, side > 0 ? new Vector3(0f, 0.7f, -1.9f) : new Vector3(0f, 2.7f, 2.6f));

            var hand = BuildHand("Hand", root.transform, rest.transform);
            var seat = root.AddComponent<SeatView>();
            seat.EditorSetup(cup, hand, kept.transform, shake.transform, bubble.transform);
            return seat;
        }

        /// <summary>
        /// Рука-заглушка: ладонь и пальцы из примитивов, точка захвата стакана. Ладонью вниз, пальцами вперёд:
        /// у правой руки большой палец слева (−X), у левой справа. Тогда при хвате стакана большой палец смотрит вверх.
        /// </summary>
        private static HandView BuildHand(string name, Transform parent, Transform rest, bool rightHanded = true)
        {
            var skin = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/M_Skin.mat");
            var root = Empty(name, parent);
            root.transform.SetPositionAndRotation(rest.position, rest.rotation);
            Prim(PrimitiveType.Sphere, "Palm", root.transform, Vector3.zero, new Vector3(0.32f, 0.14f, 0.38f), skin);
            for (var i = 0; i < 4; i++)
                Prim(PrimitiveType.Capsule, "Finger", root.transform, new Vector3(-0.1f + i * 0.066f, 0f, 0.22f),
                    new Vector3(0.06f, 0.09f, 0.06f), skin, new Vector3(90f, 0f, 0f));
            var side = rightHanded ? -1f : 1f;
            Prim(PrimitiveType.Capsule, "Thumb", root.transform, new Vector3(0.17f * side, 0f, 0.05f), new Vector3(0.07f, 0.08f, 0.07f), skin,
                new Vector3(90f, 40f * side, 0f));
            var grip = Empty("Grip", root.transform, new Vector3(0f, -0.05f, 0.2f));

            var hand = root.AddComponent<HandView>();
            hand.EditorSetup(grip.transform, rest, rightHanded);
            return hand;
        }

        /// <summary>Соперник напротив: силуэт, голова с глазами, свободная рука для жестов.</summary>
        private static OpponentAvatarView BuildOpponent()
        {
            var silhouette = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/M_Silhouette.mat");
            var eyes = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/M_Eyes.mat");

            var root = new GameObject("Opponent");
            root.transform.position = new Vector3(0f, 0f, 2.9f);
            var body = Prim(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.3f, 1.05f, 0.8f), silhouette);
            var head = Prim(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.75f, silhouette);
            foreach (var x in new[] { -0.17f, 0.17f })
                Prim(PrimitiveType.Sphere, "Eye", head.transform, new Vector3(x, 0.08f, -0.42f), Vector3.one * 0.14f, eyes);

            var accessory = Empty("AccessoryAnchor", head.transform, new Vector3(0f, 0.45f, 0f));
            accessory.transform.localScale = Vector3.one / 0.75f;

            var handRest = Empty("GestureHandRest", root.transform, new Vector3(0.9f, 0.12f, -0.8f), new Vector3(0f, 200f, 0f));
            var hand = BuildHand("GestureHand", root.transform, handRest.transform);
            var slam = Empty("SlamPoint", root.transform, new Vector3(0.7f, 0.05f, -1.1f));

            var avatar = root.AddComponent<OpponentAvatarView>();
            avatar.EditorSetup(body.transform, head.transform, accessory.transform, hand,
                new Renderer[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>() }, slam.transform);
            root.SetActive(false);
            return avatar;
        }

        private static DiceSetView BuildDice()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Dice/Die.prefab");
            var root = new GameObject("Dice");
            var views = new List<DieView>();
            for (var i = 0; i < 6; i++)
            {
                var die = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                die.name = "Die_" + i;
                die.transform.localPosition = new Vector3(i * 0.4f, -3f, 0f);
                die.SetActive(false);
                views.Add(die.GetComponent<DieView>());
            }

            var set = root.AddComponent<DiceSetView>();
            set.EditorSetup(views);
            return set;
        }

        private static CameraRig BuildCameraRig()
        {
            var root = new GameObject("CameraRig");
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(root.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.06f, 0.05f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            cameraGo.AddComponent<AudioListener>();
            var raycaster = cameraGo.AddComponent<PhysicsRaycaster>();
            raycaster.eventMask = ~(1 << IgnoreRaycastLayer);

            var shots = new GameObject("CameraShots").transform;
            var list = new List<CameraShot>
            {
                Shot(shots, CameraShots.Menu, new Vector3(3.6f, 3.2f, -5.6f), new Vector3(-1.4f, -0.2f, 0.4f), 42f),
                Shot(shots, CameraShots.Match, new Vector3(0f, 4.1f, -4.9f), new Vector3(0f, -0.2f, 0.1f), 48f),
                // Вид сверху ниже лампы (лампа на 3.6): абажур не закрывает кости, а кости крупнее на экране.
                Shot(shots, CameraShots.Top, new Vector3(0f, 3.0f, -0.35f), new Vector3(0f, 0f, 0f), 58f),
                Shot(shots, CameraShots.Opponent, new Vector3(0f, 1.9f, -1.6f), new Vector3(0f, 1.4f, 2.9f), 45f),
                Shot(shots, "shop_environment", new Vector3(6.5f, 4.5f, -9f), new Vector3(0f, 0.5f, 1.5f), 55f),
                Shot(shots, "shop_table", new Vector3(4.2f, 2.6f, -4.6f), new Vector3(0f, -0.6f, 0f), 45f),
                Shot(shots, "shop_tray", new Vector3(0f, 3.4f, -2.8f), new Vector3(0f, 0f, 0.1f), 45f),
                // Стакан — выше центра кадра и меньше: нижнюю треть экрана занимает панель магазина,
                // сверху нужен запас на показ стиля броска (рука поднимает стакан).
                Shot(shots, "shop_cup", new Vector3(3.85f, 1.85f, -3.85f), new Vector3(2.15f, 0.18f, -1.25f), 40f),
                // Показ стиля броска: от места стакана до точки тряски над лотком (стакан на высоте 1–1.9) и замах к лотку.
                // Всё — в верхних двух третях кадра, над панелью магазина.
                Shot(shots, "shop_roll", new Vector3(2.88f, 3.44f, -6.15f), new Vector3(0.9f, 0.04f, -0.5f), 45f),
                Shot(shots, "shop_dice", new Vector3(0f, 2.2f, -1.7f), new Vector3(0f, 0.1f, 0f), 40f),
                Shot(shots, "shop_lamp", new Vector3(1.8f, 2.4f, -3.2f), new Vector3(0f, 3.4f, 0f), 45f),
                Shot(shots, "shop_decor", new Vector3(-0.6f, 1.9f, -3.9f), new Vector3(-2.25f, 0.25f, -1.3f), 40f),
                // Дополнительные места безделушек: тот же сдвиг камеры от вещи, что у основного места.
                Shot(shots, "shop_decor_2", DecorSpots[1] + DecorShotOffset, DecorSpots[1] + DecorLookUp, 40f),
                Shot(shots, "shop_decor_3", DecorSpots[2] + DecorShotOffset, DecorSpots[2] + DecorLookUp, 40f),
                Shot(shots, "shop_decor_4", DecorSpots[3] + DecorShotOffset, DecorSpots[3] + DecorLookUp, 40f),
            };

            var rig = root.AddComponent<CameraRig>();
            rig.EditorSetup(camera, list);
            root.transform.SetPositionAndRotation(list[0].transform.position, list[0].transform.rotation);
            camera.fieldOfView = list[0].FieldOfView;
            return rig;
        }

        private static CameraShot Shot(Transform parent, string id, Vector3 position, Vector3 lookAt, float fov)
        {
            var go = Empty("Shot_" + id, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            var shot = go.AddComponent<CameraShot>();
            shot.EditorSetup(id, fov);
            return shot;
        }

        /// <summary>Экранный канвас 1920×1080 в режиме Expand и приёмник нажатий на объекте go.</summary>
        private static Canvas AddScaledCanvas(GameObject go)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand: канвас всегда не меньше 1920×1080 по обеим осям, лишнее место добавляется по краям.
            // Всё, что свёрстано в 1920×1080, видно на любом экране: квадратном, узком, вертикальном, сверхшироком.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>ZonkInstaller в ProjectContext получает реестр, настройки и тексты.</summary>
        private static void SetupProjectContext(ContentSet content)
        {
            const string path = "Assets/Resources/ProjectContext.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var installer = root.GetComponent<ZonkInstaller>();
                if (installer == null)
                {
                    Debug.LogError("[Zonk] ZonkInstaller not found in " + path);
                    return;
                }

                installer.EditorSetup(content.Database, content.Config, content.Texts);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Bootstrap первой, затем стол. Тестовая сцена площадок остаётся в списке выключенной.</summary>
        private static void SetupBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapScene, true),
                new EditorBuildSettingsScene(TableScene, true),
            };

            const string platformTest = "Assets/Scenes/PlatformTest.unity";
            if (System.IO.File.Exists(platformTest))
                scenes.Add(new EditorBuildSettingsScene(platformTest, false));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
