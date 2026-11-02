using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Рука игрока (07.10.2026): модель Art/Models/Hands/Hand.fbx из Tools/Blender/zonk_arms.py — кисть с куском
    /// предплечья, пальцы на костях. Префаб Prefabs/Hands/Hand.prefab: модель без аниматора, материалы кожи
    /// и заглушки среза (Toon), точка хвата, якорь рукава «Sleeve» (слот hand_sleeve, ZonkSetup.HandLook), HandView
    /// с пальцами и HandConfig. Сцена ставит его вместо заглушки из примитивов;
    /// нет FBX (скрипт Blender не запускали) — остаётся заглушка. Префаб создаётся один раз: ручные правки не затираются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string HandModelPath = Models + "/Hands/Hand.fbx";
        private const string HandPrefabPath = Prefabs + "/Hands/Hand.prefab";

        /// <summary>Пальцы модели по порядку суставов: имена костей из zonk_arms.py.</summary>
        private static readonly string[] HandFingerNames = { "Index", "Middle", "Ring", "Pinky", "Thumb" };

        private static readonly Color HandSkinColor = new Color(0.93f, 0.72f, 0.58f);
        private static readonly Color HandCuffColor = new Color(0.12f, 0.1f, 0.1f);

        /// <summary>Генератор: префаб руки до перевода материалов на Toon (иначе её материалы останутся на Lit до следующего раза).</summary>
        private static void BuildHands()
        {
            if (!(AssetImporter.GetAtPath(HandModelPath) is ModelImporter importer))
                return;

            ConfigureHandModel(importer);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HandModelPath);
            var config = Asset<HandConfig>(ConfigsFolder + "/Game/Hand.asset", c => { });
            var skin = Mat(null, "Hand_Skin", HandSkinColor, smoothness: 0.25f);
            var cuff = Mat(null, "Hand_Cuff", HandCuffColor, smoothness: 0.15f);

            // Первый префаб (07.10) собран из модели без скелета и развёрнутым: пересоздать один раз.
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            if (old != null && System.Array.IndexOf(AssetDatabase.GetLabels(old), HandPrefabLabel) < 0)
                AssetDatabase.DeleteAsset(HandPrefabPath);

            var prefab = Prefab(HandPrefabPath, () =>
            {
                var root = new GameObject("Hand");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Model";
                instance.transform.SetParent(root.transform, false);
                AlignHandModel(root.transform, instance.transform);

                // Пальцы гнёт HandView: аниматор модели (Generic) не нужен.
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    animator.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                }

                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (renderer != null)
                {
                    renderer.sharedMaterials = new[] { skin, cuff };
                    renderer.updateWhenOffscreen = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                else
                {
                    Debug.LogError("[Zonk] " + HandModelPath + ": no SkinnedMeshRenderer — fingers will not bend. Check Rig: Generic");
                }

                var fingers = new List<HandFinger>();
                foreach (var finger in HandFingerNames)
                {
                    var joints = new Transform[3];
                    for (var i = 0; i < joints.Length; i++)
                        joints[i] = FindDeep(instance.transform, finger + (i + 1));
                    if (System.Array.IndexOf(joints, null) >= 0)
                    {
                        Debug.LogWarning("[Zonk] " + HandModelPath + ": no bones for finger " + finger + " — it will not bend");
                        continue;
                    }

                    fingers.Add(new HandFinger { Joints = joints, Thumb = finger == "Thumb" });
                }

                var grip = Empty("Grip", root.transform);
                var hand = root.AddComponent<HandView>();
                hand.EditorSetup(grip.transform, null);
                hand.EditorSetupRig(config, fingers.ToArray());
                return root;
            });

            if (prefab != null && System.Array.IndexOf(AssetDatabase.GetLabels(prefab), HandPrefabLabel) < 0)
                AssetDatabase.SetLabels(prefab, new[] { HandPrefabLabel });

            // Заглушка среза под рукавом — тёмная (раньше светлая манжета).
            if (cuff.HasProperty("_BaseColor") && cuff.GetColor("_BaseColor") != HandCuffColor)
            {
                cuff.SetColor("_BaseColor", HandCuffColor);
                EditorUtility.SetDirty(cuff);
            }

            AddLookAnchorsToHand();
        }

        /// <summary>
        /// Облик руки в префабе (и в созданном раньше): якоря «Sleeve», «Wrist», «HandLook» в корне руки (слоты рукава,
        /// запястья, кисти; модели выгружены в осях руки, смещение не нужно), рендерер кожи и слоты у HandView.
        /// Уже заданное не меняется.
        /// </summary>
        private static void AddLookAnchorsToHand()
        {
            var sleeveSlot = SlotAt(HandSleeveSlotId);
            var wristSlot = SlotAt(HandWristSlotId);
            var lookSlot = SlotAt(HandLookSlotId);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath) == null || sleeveSlot == null || wristSlot == null ||
                lookSlot == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(HandPrefabPath);
            try
            {
                var hand = root.GetComponent<HandView>();
                if (hand == null || (hand.Sleeve != null && hand.Wrist != null && hand.HandLook != null))
                    return;

                CosmeticAnchor AnchorAt(string name, string slotId)
                {
                    var go = root.transform.Find(name);
                    var anchor = go != null ? go.GetComponent<CosmeticAnchor>() : null;
                    if (anchor != null)
                        return anchor;
                    anchor = Empty(name, root.transform).AddComponent<CosmeticAnchor>();
                    anchor.EditorSetup(slotId);
                    return anchor;
                }

                hand.EditorSetupLook(root.GetComponentInChildren<SkinnedMeshRenderer>(true), AnchorAt("Sleeve", HandSleeveSlotId),
                    sleeveSlot);
                hand.EditorSetupParts(AnchorAt("Wrist", HandWristSlotId), wristSlot, AnchorAt("HandLook", HandLookSlotId), lookSlot);
                PrefabUtility.SaveAsPrefabAsset(root, HandPrefabPath);
                Debug.Log("[Zonk] Hand: look anchors added (sleeve, wrist, hand)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private const string HandPrefabLabel = "zonk-hand-v2";

        /// <summary>
        /// Импорт руки: без материалов и анимаций, оси Blender → Unity запекаются. Риг Generic: с риггингом None
        /// Unity импортирует модель без скелета (обычный MeshRenderer) — пальцы не гнутся. Аниматор отключается в префабе.
        /// </summary>
        private static void ConfigureHandModel(ModelImporter importer)
        {
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation &&
                importer.animationType == ModelImporterAnimationType.Generic && importer.bakeAxisConversion && !importer.isReadable)
                return;

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.bakeAxisConversion = true;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Развернуть модель в осях руки по её костям, а не по договорённости об осях Blender → Unity (у модели со скелетом
        /// они переходят иначе, чем у стаканов): пальцы (запястье → средний палец) — +Z, мизинец — +X, большой палец — −X,
        /// ладонь — −Y. Центр ладони — ноль модели (zonk_arms.py), он при повороте остаётся на месте.
        /// </summary>
        private static void AlignHandModel(Transform root, Transform model)
        {
            var palm = FindDeep(model, "Palm");
            var middle = FindDeep(model, "Middle1");
            var index = FindDeep(model, "Index1");
            var pinky = FindDeep(model, "Pinky1");
            var thumb = FindDeep(model, "Thumb3");
            if (palm == null || middle == null || index == null || pinky == null)
            {
                Debug.LogError("[Zonk] " + HandModelPath + ": no Palm / Middle1 / Index1 / Pinky1 bones — run Tools/Blender/zonk_arms.py");
                return;
            }

            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            var forward = root.InverseTransformDirection(middle.position - palm.position).normalized;
            var right = Vector3.ProjectOnPlane(root.InverseTransformDirection(pinky.position - index.position), forward).normalized;
            var up = Vector3.Cross(forward, right);
            model.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);

            // Большой палец свисает под ладонь: если он выше — модель зеркальная (левая рука), хват будет наизнанку.
            if (thumb != null && root.InverseTransformPoint(thumb.position).y > 0f)
                Debug.LogWarning("[Zonk] " + HandModelPath + ": thumb above the palm — mirrored model? Expected a right hand");
        }

        /// <summary>Рука из префаба на месте покоя; нет префаба — null (сцена строит заглушку).</summary>
        private static HandView InstantiateHand(string name, Transform parent, Transform rest, bool rightHanded)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandPrefabPath);
            if (prefab == null)
                return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(rest.position, rest.rotation);
            var hand = go.GetComponent<HandView>();
            hand.EditorSetup(hand.Grip, rest, rightHanded);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hand);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            return hand;
        }
    }
}
