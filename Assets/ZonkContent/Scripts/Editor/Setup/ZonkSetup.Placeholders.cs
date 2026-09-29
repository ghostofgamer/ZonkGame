using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Заглушки из примитивов: стол, сукно, лампы, локации, аксессуары соперников.
    /// Размеры согласованы со сценой: верх стола на y = 0, пол на y = −1.6, лоток 3.2 × 2.
    /// Настоящие модели подставляются в конфиги предметов вместо этих префабов.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const float FloorY = -1.6f;

        private static void BuildPlaceholders(ArtSet art)
        {
            art.Table = Prefab(Prefabs + "/Table/Table_Oak.prefab", () =>
            {
                var root = new GameObject("Table_Oak");
                Prim(PrimitiveType.Cube, "Top", root.transform, new Vector3(0f, -0.1f, 0f), new Vector3(5.6f, 0.2f, 4f), art["Table_Oak"]);
                foreach (var x in new[] { -2.5f, 2.5f })
                foreach (var z in new[] { -1.7f, 1.7f })
                    Prim(PrimitiveType.Cylinder, "Leg", root.transform, new Vector3(x, (FloorY - 0.2f) * 0.5f, z),
                        new Vector3(0.22f, (-FloorY - 0.2f) * 0.5f, 0.22f), art["Table_Oak"]);
                return root;
            });

            art.Felt = Prefab(Prefabs + "/Table/Felt_Green.prefab", () =>
            {
                var root = new GameObject("Felt_Green");
                Prim(PrimitiveType.Cube, "Felt", root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(3.2f, 0.02f, 2f), art["Felt_Green"]);
                return root;
            });

            art.LampBasic = Prefab(Prefabs + "/Lamps/Lamp_Basic.prefab", () =>
            {
                var root = new GameObject("Lamp_Basic");
                Prim(PrimitiveType.Cylinder, "Cord", root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.03f, 1f, 0.03f), art["Black"]);
                Prim(PrimitiveType.Cylinder, "Shade", root.transform, Vector3.zero, new Vector3(1.2f, 0.16f, 1.2f), art["Lamp"]);
                Prim(PrimitiveType.Sphere, "Bulb", root.transform, new Vector3(0f, -0.15f, 0f), Vector3.one * 0.28f, art["Bulb"]);
                AddLight(root.transform, new Vector3(0f, -0.35f, 0f), new Color(1f, 0.85f, 0.65f), 3.2f, 9f);
                return root;
            });

            art.LampLantern = Prefab(Prefabs + "/Lamps/Lamp_Lantern.prefab", () =>
            {
                var root = new GameObject("Lamp_Lantern");
                Prim(PrimitiveType.Cylinder, "Chain", root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.04f, 1f, 0.04f), art["Metal"]);
                Prim(PrimitiveType.Cube, "Cap", root.transform, new Vector3(0f, 0.32f, 0f), new Vector3(0.5f, 0.08f, 0.5f), art["Metal"]);
                Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, -0.32f, 0f), new Vector3(0.5f, 0.08f, 0.5f), art["Metal"]);
                foreach (var x in new[] { -0.22f, 0.22f })
                foreach (var z in new[] { -0.22f, 0.22f })
                    Prim(PrimitiveType.Cube, "Post", root.transform, new Vector3(x, 0f, z), new Vector3(0.05f, 0.6f, 0.05f), art["Metal"]);
                Prim(PrimitiveType.Sphere, "Flame", root.transform, Vector3.zero, new Vector3(0.18f, 0.28f, 0.18f), art["Candle"]);
                AddLight(root.transform, new Vector3(0f, -0.1f, 0f), new Color(1f, 0.65f, 0.35f), 3.5f, 8f);
                return root;
            });

            art.EnvHome = Prefab(Prefabs + "/Environments/Env_Home.prefab", () =>
            {
                var root = Room("Env_Home", art["Floor_Wood"], art["Wall_Home"]);
                Prim(PrimitiveType.Cube, "Window", root.transform, new Vector3(-3.5f, 1.4f, 7.85f), new Vector3(3f, 2f, 0.05f), art["Window"]);
                Prim(PrimitiveType.Cube, "WindowFrame", root.transform, new Vector3(-3.5f, 1.4f, 7.88f), new Vector3(3.3f, 2.3f, 0.05f), art["Frame"]);
                Prim(PrimitiveType.Cylinder, "Rug", root.transform, new Vector3(0f, FloorY + 0.01f, 0f), new Vector3(8f, 0.01f, 6f), art["Rug"]);
                Prim(PrimitiveType.Cube, "Shelf", root.transform, new Vector3(4f, 0.8f, 7.6f), new Vector3(3f, 0.1f, 0.6f), art["Frame"]);
                Prim(PrimitiveType.Cube, "Chest", root.transform, new Vector3(5.5f, FloorY + 0.45f, 5.5f), new Vector3(1.6f, 0.9f, 1f), art["Barrel"]);
                return root;
            });

            art.EnvTavern = Prefab(Prefabs + "/Environments/Env_Tavern.prefab", () =>
            {
                var root = Room("Env_Tavern", art["Floor_Wood"], art["Stone"]);
                Prim(PrimitiveType.Cube, "Bar", root.transform, new Vector3(0f, FloorY + 0.6f, 7f), new Vector3(9f, 1.2f, 1f), art["Barrel"]);
                foreach (var x in new[] { -6.5f, -5.3f, 5.5f })
                    Prim(PrimitiveType.Cylinder, "Barrel", root.transform, new Vector3(x, FloorY + 0.6f, 5.8f), new Vector3(1f, 0.6f, 1f), art["Barrel"]);
                foreach (var x in new[] { -3f, 3f })
                {
                    Prim(PrimitiveType.Cylinder, "Candle", root.transform, new Vector3(x, FloorY + 1.35f, 7f), new Vector3(0.12f, 0.15f, 0.12f), art["Candle"]);
                    AddLight(root.transform, new Vector3(x, FloorY + 1.7f, 6.8f), new Color(1f, 0.6f, 0.3f), 1.5f, 6f);
                }

                return root;
            });

            art.EnvBeach = Prefab(Prefabs + "/Environments/Env_Beach.prefab", () =>
            {
                var root = new GameObject("Env_Beach");
                Prim(PrimitiveType.Plane, "Sand", root.transform, new Vector3(0f, FloorY, 0f), new Vector3(4f, 1f, 3f), art["Sand"]);
                Prim(PrimitiveType.Plane, "Sea", root.transform, new Vector3(0f, FloorY - 0.05f, 30f), new Vector3(12f, 1f, 4f), art["Sea"]);
                Prim(PrimitiveType.Quad, "Sky", root.transform, new Vector3(0f, 8f, 50f), new Vector3(160f, 50f, 1f), art["Sky"]);
                Prim(PrimitiveType.Sphere, "Sun", root.transform, new Vector3(-12f, 12f, 45f), Vector3.one * 5f, art["Sun"]);
                foreach (var position in new[] { new Vector3(-6f, 0f, 6f), new Vector3(7f, 0f, 8f) })
                {
                    Prim(PrimitiveType.Cylinder, "Trunk", root.transform, position + new Vector3(0f, FloorY + 2.5f, 0f), new Vector3(0.35f, 2.5f, 0.35f), art["Trunk"], new Vector3(0f, 0f, 8f));
                    for (var i = 0; i < 5; i++)
                        Prim(PrimitiveType.Sphere, "Leaf", root.transform, position + new Vector3(0f, FloorY + 5f, 0f),
                            new Vector3(3f, 0.25f, 0.9f), art["Palm"], new Vector3(-15f, i * 72f, 0f));
                }

                return root;
            });

            art.EnvShip = Prefab(Prefabs + "/Environments/Env_Ship.prefab", () =>
            {
                var root = new GameObject("Env_Ship");
                Prim(PrimitiveType.Cube, "Deck", root.transform, new Vector3(0f, FloorY - 0.1f, 2f), new Vector3(14f, 0.2f, 22f), art["Deck"]);
                Prim(PrimitiveType.Plane, "Sea", root.transform, new Vector3(0f, FloorY - 2f, 0f), new Vector3(30f, 1f, 30f), art["Sea"]);
                Prim(PrimitiveType.Quad, "Sky", root.transform, new Vector3(0f, 8f, 60f), new Vector3(200f, 60f, 1f), art["Sky"]);
                Prim(PrimitiveType.Cylinder, "Mast", root.transform, new Vector3(0f, 4f, 7f), new Vector3(0.5f, 6f, 0.5f), art["Trunk"]);
                Prim(PrimitiveType.Cube, "Sail", root.transform, new Vector3(0f, 5.5f, 6.6f), new Vector3(6f, 5f, 0.05f), art["Sail"]);
                foreach (var x in new[] { -6.8f, 6.8f })
                    Prim(PrimitiveType.Cube, "Rail", root.transform, new Vector3(x, FloorY + 0.6f, 2f), new Vector3(0.2f, 1.2f, 22f), art["Barrel"]);
                return root;
            });

            art.HatTop = Prefab(Prefabs + "/Accessories/Hat_Top.prefab", () =>
            {
                var root = new GameObject("Hat_Top");
                Prim(PrimitiveType.Cylinder, "Brim", root.transform, Vector3.zero, new Vector3(0.95f, 0.02f, 0.95f), art["Black"]);
                Prim(PrimitiveType.Cylinder, "Crown", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.25f, 0.55f), art["Black"]);
                return root;
            });

            art.HatPirate = Prefab(Prefabs + "/Accessories/Hat_Pirate.prefab", () =>
            {
                var root = new GameObject("Hat_Pirate");
                Prim(PrimitiveType.Cube, "Hat", root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.22f, 0.55f), art["Black"]);
                Prim(PrimitiveType.Cube, "Band", root.transform, new Vector3(0f, 0.03f, -0.2f), new Vector3(0.9f, 0.08f, 0.2f), art["Red"]);
                return root;
            });

            art.Hood = Prefab(Prefabs + "/Accessories/Hood.prefab", () =>
            {
                var root = new GameObject("Hood");
                Prim(PrimitiveType.Sphere, "Hood", root.transform, new Vector3(0f, -0.25f, 0.08f), new Vector3(0.9f, 0.85f, 0.9f), art["Red"]);
                return root;
            });
        }

        /// <summary>Комната: пол и три стены (со стороны камеры стены нет).</summary>
        private static GameObject Room(string name, Material floor, Material wall)
        {
            var root = new GameObject(name);
            Prim(PrimitiveType.Plane, "Floor", root.transform, new Vector3(0f, FloorY, 0f), new Vector3(2f, 1f, 2f), floor);
            Prim(PrimitiveType.Cube, "WallBack", root.transform, new Vector3(0f, 2.4f, 8f), new Vector3(20f, 8f, 0.2f), wall);
            Prim(PrimitiveType.Cube, "WallLeft", root.transform, new Vector3(-9f, 2.4f, 0f), new Vector3(0.2f, 8f, 20f), wall);
            Prim(PrimitiveType.Cube, "WallRight", root.transform, new Vector3(9f, 2.4f, 0f), new Vector3(0.2f, 8f, 20f), wall);
            return root;
        }

        private static void AddLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            var go = Empty("Light", parent, position);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
