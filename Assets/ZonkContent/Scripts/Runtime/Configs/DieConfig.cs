using System.Collections.Generic;
using UnityEngine;
using Zonk.Core.Dice;

namespace Zonk.Configs
{
    /// <summary>
    /// Механика кости: веса граней. Внешний вид задаёт скин (CosmeticItemConfig слота dice_skin),
    /// поэтому любая кость носит любой скин. Особую кость видно по цвету метки при любом скине.
    /// Баланс проверяется в окне Zonk/Balance Simulator: средний результат набора в пределах ±7% от обычного.
    /// </summary>
    [CreateAssetMenu(menuName = "Zonk/Die", fileName = "Die")]
    public sealed class DieConfig : ContentConfig
    {
        [Tooltip("Ключ описания: чем кость отличается")]
        public string DescriptionKey;

        [Tooltip("Относительные веса граней 1..6. Обычная кость: все по 1.")]
        public float[] Weights = { 1, 1, 1, 1, 1, 1 };

        [Tooltip("Порядок в магазине")]
        public int Order;

        [Tooltip("Цвет метки особой кости. Прозрачный = без метки.")]
        public Color MarkerColor = Color.clear;

        [Tooltip("Свой вид особой кости: материал (и при желании меш). Перекрывает скин игрока, чтобы кость узнавалась. Пусто = скин игрока")]
        public Material LookMaterial;

        public Mesh LookMesh;

        [Tooltip("Вид кости на уровнях мастерства: [0] — первый уровень и т.д. Пусто = LookMaterial")]
        public List<Material> MasteryLooks = new List<Material>();

        [Tooltip("Как получить кость. Пусто = есть у всех с начала.")]
        public Price Price = new Price();

        private DieSpec _spec;

        public bool IsSpecial => MarkerColor.a > 0f;

        public DieSpec Spec
        {
            get
            {
                if (_spec == null)
                {
                    var weights = new double[DieSpec.FaceCount];
                    for (var i = 0; i < weights.Length; i++)
                        weights[i] = Weights != null && i < Weights.Length ? Weights[i] : 1;
                    _spec = new DieSpec(Id, weights);
                }

                return _spec;
            }
        }

        private void OnValidate()
        {
            _spec = null;
        }
    }
}
