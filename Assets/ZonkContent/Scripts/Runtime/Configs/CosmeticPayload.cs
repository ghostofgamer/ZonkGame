using System;
using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Ai;
using Zonk.Core.Modifiers;
using Zonk.Presentation;

namespace Zonk.Configs
{
    /// <summary>Что именно даёт предмет косметики.</summary>
    [Serializable]
    public abstract class CosmeticPayload
    {
    }

    /// <summary>Своя модель: префаб ставится в якорь вместо текущего.</summary>
    [Serializable]
    public sealed class PrefabPayload : CosmeticPayload
    {
        public GameObject Prefab;

        [Tooltip("Освещение и атмосфера сцены с этим предметом (для локаций; у слота должно быть DrivesLighting). Пусто — GameConfig.DefaultLighting")]
        public LightingProfileConfig Lighting;
    }

    /// <summary>Только материал (текстура, цвет) на базовой модели слота.</summary>
    [Serializable]
    public sealed class MaterialPayload : CosmeticPayload
    {
        public Material Material;
    }

    /// <summary>Меш и материал. Для костей: меш пустой = базовая модель кости.</summary>
    [Serializable]
    public sealed class MeshMaterialPayload : CosmeticPayload
    {
        public Mesh Mesh;
        public Material Material;
    }

    /// <summary>Стиль броска: как рука трясёт стакан и высыпает кости. Только внешний вид, на грани не влияет.</summary>
    [Serializable]
    public sealed class RollStylePayload : CosmeticPayload
    {
        public RollStyleConfig Style;
    }
}
