using UnityEditor;
using UnityEngine;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Таверна «Пьяный якорь» со светом, запечённым в Tools/Blender/zonk_tavern.py: модель Room_Tavern, текстуры Tavern_Room, Tavern_Props.
    /// Живые части поверх: огонь очага, дымок, отсвет на полу, свечи на стене, ореолы фонарей. Положения те же, что в скрипте.
    /// Префаб Env_Tavern остаётся тем же файлом. «Таверна в полночь» — та же модель, темнее (BakedTint профиля).
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string TavernModel = Root + "/Art/Models/Environments/Room_Tavern.fbx";
        private const float TavernFireX = -4.6f;
        private const float TavernFireZ = 4.33f;
        private const float TavernWallZ = 4.7f;
        private const float TavernCandleY = 0.96f;

        [MenuItem("Zonk/Art/Apply Tavern Room", priority = 47)]
        public static void ApplyTavernRoomMenu()
        {
            AssetDatabase.Refresh();
            ImportComicSprites();
            ApplyTavernRoom();
            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Tavern room applied");
        }

        private static void ApplyTavernRoom()
        {
            var room = BakedMaterial("M_Tavern_Room", "Tavern_Room", 2048);
            var props = BakedMaterial("M_Tavern_Props", "Tavern_Props", 1024);
            var smoke = SmokeWispMaterial();
            ApplyBakedRoom(Prefabs + "/Environments/Env_Tavern.prefab", TavernModel, "zonk_tavern.py", room, props, root =>
            {
                AddFlames(root, TavernFireX, TavernFireZ);
                AddSmoke(root, smoke, TavernFireX, TavernFireZ);
                AddFloorGlow(root, TavernFireX, 2.7f);
                AddCandle(root, new Vector3(-1.5f, TavernCandleY, TavernWallZ));
                AddCandle(root, new Vector3(1.6f, TavernCandleY, TavernWallZ));
                AddLanternGlow(root, new Vector3(-2.6f, 2.5f, 2.3f), 1.9f);
                AddLanternGlow(root, new Vector3(4.6f, 2.7f, -0.4f), 2.3f);
                AddLanternGlow(root, new Vector3(-5f, 2.6f, -2.2f), 1.6f);
            });

            SetBakedTint("Lighting_TavernNight", new Color(0.5f, 0.56f, 0.82f));
        }

        /// <summary>Ореол фонаря: мягкое пятно дышит, как живой огонь. Без источника света.</summary>
        private static void AddLanternGlow(Transform root, Vector3 position, float speed)
        {
            var sprite = SpriteAt(Root + "/Art/Textures/Particle_SoftDot.png");
            if (sprite == null)
                return;

            var go = SpriteObject("LanternGlow", root, position, 1f, sprite);
            go.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            var size = sprite.bounds.size;
            go.transform.localScale = new Vector3(1.1f / Mathf.Max(size.x, 0.01f), 1.1f / Mathf.Max(size.y, 0.01f), 1f);
            go.AddComponent<WorldGlowPulse>().EditorSetup(go.GetComponent<SpriteRenderer>(), new Color(1f, 0.62f, 0.25f, 0.3f), 0.4f, speed);
        }
    }
}
