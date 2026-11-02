using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Лёгкие сфера и капсула для заглушек из примитивов (05.10). Встроенная сфера Unity — 768 треугольников, капсула — 832:
    /// руки, соперник и листья пальм на пляже давали ~27 тыс. треугольников — больше стола, стаканов и рамки вместе.
    /// Здесь те же размеры (сфера диаметром 1, капсула 1 × 2 по Y), 10 сегментов: ~100 и ~140 треугольников, на вид
    /// под комиксовым шейдером то же. Prim ставит их сам; готовые префабы переводит SlimPrimitiveMeshes.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string LowPolyFolder = Root + "/Art/Models/Primitives";
        private const int LowPolySegments = 10;

        private static Mesh _lowSphere;
        private static Mesh _lowCapsule;

        /// <summary>Лёгкий меш вместо встроенного (сфера, капсула); для остальных — null (встроенные и так лёгкие).</summary>
        private static Mesh LowPolyMesh(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere:
                    return _lowSphere != null ? _lowSphere : _lowSphere = LowPolyAsset("Low_Sphere", 0f, 6);
                case PrimitiveType.Capsule:
                    return _lowCapsule != null ? _lowCapsule : _lowCapsule = LowPolyAsset("Low_Capsule", 1f, 3);
                default:
                    return null;
            }
        }

        /// <summary>Тело вращения по Y радиусом 0.5: полусферы по rings колец, между ними цилиндр длиной straight.</summary>
        private static Mesh LowPolyAsset(string name, float straight, int rings)
        {
            EnsureFolder(LowPolyFolder);
            var path = LowPolyFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
                return existing;

            const float radius = 0.5f;
            var profile = new List<Vector2>();   // (радиус, высота) снизу вверх
            var half = straight * 0.5f;
            var hemisphere = straight > 0f ? rings : rings / 2;
            for (var i = 0; i <= hemisphere; i++)
            {
                var a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / hemisphere;
                profile.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius - half));
            }

            for (var i = straight > 0f ? 0 : 1; i <= hemisphere; i++)
            {
                var a = Mathf.PI * 0.5f * i / hemisphere;
                profile.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius + half));
            }

            var segments = LowPolySegments;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var total = half * 2f + radius * 2f;
            for (var r = 0; r < profile.Count; r++)
            {
                for (var s = 0; s <= segments; s++)
                {
                    var angle = 2f * Mathf.PI * s / segments;
                    var p = profile[r];
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices.Add(direction * p.x + Vector3.up * p.y);
                    var centerY = p.y > half ? half : p.y < -half ? -half : p.y;
                    normals.Add((vertices[vertices.Count - 1] - Vector3.up * centerY).normalized);
                    uvs.Add(new Vector2((float)s / segments, (p.y + total * 0.5f) / total));
                }
            }

            var triangles = new List<int>();
            for (var r = 0; r < profile.Count - 1; r++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var a = r * (segments + 1) + s;
                    var b = a + segments + 1;
                    if (profile[r].x > 1e-4f)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(a + 1);
                    }

                    if (profile[r + 1].x > 1e-4f)
                    {
                        triangles.Add(a + 1);
                        triangles.Add(b);
                        triangles.Add(b + 1);
                    }
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Встроенные сферы и капсулы в готовых префабах — на лёгкие (заблокированные префабы не трогаются).</summary>
        private static void SlimPrimitiveMeshes()
        {
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            var capsule = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
            var changedPrefabs = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !UsesMesh(asset, sphere, capsule) || SkipIfLocked(path))
                    continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh == sphere)
                            filter.sharedMesh = LowPolyMesh(PrimitiveType.Sphere);
                        else if (filter.sharedMesh == capsule)
                            filter.sharedMesh = LowPolyMesh(PrimitiveType.Capsule);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedPrefabs++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (changedPrefabs > 0)
                Debug.Log("[Zonk] Low-poly primitives: " + changedPrefabs + " prefabs");
        }

        private static bool UsesMesh(GameObject asset, Mesh sphere, Mesh capsule)
        {
            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == sphere || filter.sharedMesh == capsule)
                    return true;
            }

            return false;
        }
    }
}
