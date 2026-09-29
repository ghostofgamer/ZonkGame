using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor
{
    /// <summary>
    /// Цена предмета в инспекторе галочками: «За монеты», «За рекламу», «За деньги», «Награда кампании».
    /// Отметил — вариант появился, снял — пропал. Обычно у предмета отмечен один способ (правило магазина:
    /// во вкладке поровну за монеты, рекламу и деньги). ID товара за деньги по умолчанию = ID предмета.
    /// Другие, новые виды цены (наследники PriceOption) показываются ниже обычным списком.
    /// Ни одной галочки — предмет бесплатный, он есть у всех (валидатор предупреждает).
    /// </summary>
    [CustomPropertyDrawer(typeof(Price))]
    public sealed class PriceDrawer : PropertyDrawer
    {
        private const float Gap = 2f;
        private static readonly Type[] Known =
            { typeof(CurrencyPriceOption), typeof(RewardedAdPriceOption), typeof(PurchasePriceOption), typeof(ProgressPriceOption) };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight + Gap;
            var height = line * 5;
            var options = property.FindPropertyRelative("Options");
            if (HasOther(options))
                height += EditorGUI.GetPropertyHeight(options, true) + Gap;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var options = property.FindPropertyRelative("Options");
            var line = EditorGUIUtility.singleLineHeight;
            var row = new Rect(position.x, position.y, position.width, line);

            EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
            if (options.arraySize == 0)
            {
                var note = new Rect(row.x + EditorGUIUtility.labelWidth, row.y, row.width - EditorGUIUtility.labelWidth, line);
                EditorGUI.LabelField(note, "бесплатно: есть у всех", EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel++;
            row.y += line + Gap;
            DrawCoins(ref row, options);
            DrawAds(ref row, options);
            DrawPurchase(ref row, options, property);
            DrawProgress(ref row, options);
            EditorGUI.indentLevel--;

            if (HasOther(options))
            {
                var rest = new Rect(position.x, row.y, position.width, EditorGUI.GetPropertyHeight(options, true));
                EditorGUI.PropertyField(rest, options, new GUIContent("Все варианты (есть особые)"), true);
            }
        }

        private static void DrawCoins(ref Rect row, SerializedProperty options)
        {
            var element = Find(options, typeof(CurrencyPriceOption));
            if (Toggle(ref row, "За монеты", element, out var valueRect) != (element != null))
            {
                if (element == null)
                    Add(options, new CurrencyPriceOption { Currency = DefaultCoins(), Amount = 1000 });
                else
                    Remove(options, element);
                return;
            }

            if (element != null)
            {
                var half = valueRect.width * 0.5f;
                EditorGUI.PropertyField(new Rect(valueRect.x, valueRect.y, half - 4f, valueRect.height),
                    element.FindPropertyRelative("Amount"), GUIContent.none);
                EditorGUI.PropertyField(new Rect(valueRect.x + half, valueRect.y, half, valueRect.height),
                    element.FindPropertyRelative("Currency"), GUIContent.none);
            }
        }

        private static void DrawAds(ref Rect row, SerializedProperty options)
        {
            var element = Find(options, typeof(RewardedAdPriceOption));
            if (Toggle(ref row, "За рекламу (раз)", element, out var valueRect) != (element != null))
            {
                if (element == null)
                    Add(options, new RewardedAdPriceOption { AdsRequired = 4 });
                else
                    Remove(options, element);
                return;
            }

            if (element != null)
                EditorGUI.PropertyField(valueRect, element.FindPropertyRelative("AdsRequired"), GUIContent.none);
        }

        private static void DrawPurchase(ref Rect row, SerializedProperty options, SerializedProperty price)
        {
            var element = Find(options, typeof(PurchasePriceOption));
            if (Toggle(ref row, "За деньги (ID товара)", element, out var valueRect) != (element != null))
            {
                if (element == null)
                    Add(options, new PurchasePriceOption { ProductId = OwnerId(price) });
                else
                    Remove(options, element);
                return;
            }

            if (element != null)
            {
                var product = element.FindPropertyRelative("ProductId");
                if (string.IsNullOrEmpty(product.stringValue))
                    product.stringValue = OwnerId(price);
                EditorGUI.PropertyField(valueRect, product, GUIContent.none);
            }
        }

        private static void DrawProgress(ref Rect row, SerializedProperty options)
        {
            var element = Find(options, typeof(ProgressPriceOption));
            if (Toggle(ref row, "Награда кампании (подсказка)", element, out var valueRect) != (element != null))
            {
                if (element == null)
                    Add(options, new ProgressPriceOption());
                else
                    Remove(options, element);
                return;
            }

            if (element != null)
                EditorGUI.PropertyField(valueRect, element.FindPropertyRelative("HintKey"), GUIContent.none);
        }

        /// <summary>Строка с галочкой: слева галочка с подписью, справа место под значение. Возвращает новое состояние.</summary>
        private static bool Toggle(ref Rect row, string label, SerializedProperty element, out Rect valueRect)
        {
            var toggleWidth = EditorGUIUtility.labelWidth;
            var toggleRect = new Rect(row.x, row.y, toggleWidth, row.height);
            valueRect = new Rect(row.x + toggleWidth, row.y, row.width - toggleWidth, row.height);
            var value = EditorGUI.ToggleLeft(toggleRect, label, element != null);
            row.y += row.height + Gap;
            return value;
        }

        private static SerializedProperty Find(SerializedProperty options, Type type)
        {
            for (var i = 0; i < options.arraySize; i++)
            {
                var element = options.GetArrayElementAtIndex(i);
                if (element.managedReferenceValue != null && element.managedReferenceValue.GetType() == type)
                    return element;
            }

            return null;
        }

        private static bool HasOther(SerializedProperty options)
        {
            for (var i = 0; i < options.arraySize; i++)
            {
                var value = options.GetArrayElementAtIndex(i).managedReferenceValue;
                if (value == null || !Known.Contains(value.GetType()))
                    return true;
            }

            return false;
        }

        private static void Add(SerializedProperty options, PriceOption option)
        {
            var index = options.arraySize;
            options.InsertArrayElementAtIndex(index);
            options.GetArrayElementAtIndex(index).managedReferenceValue = option;
            options.serializedObject.ApplyModifiedProperties();
        }

        private static void Remove(SerializedProperty options, SerializedProperty element)
        {
            for (var i = 0; i < options.arraySize; i++)
            {
                if (SerializedProperty.EqualContents(options.GetArrayElementAtIndex(i), element))
                {
                    options.GetArrayElementAtIndex(i).managedReferenceValue = null;
                    options.DeleteArrayElementAtIndex(i);
                    options.serializedObject.ApplyModifiedProperties();
                    return;
                }
            }
        }

        private static string OwnerId(SerializedProperty price)
        {
            return price.serializedObject.targetObject is ContentConfig content ? content.Id : string.Empty;
        }

        private static CurrencyConfig DefaultCoins()
        {
            return AssetDatabase.FindAssets("t:" + nameof(CurrencyConfig))
                .Select(g => AssetDatabase.LoadAssetAtPath<CurrencyConfig>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(c => c != null && c.Id == "coins");
        }
    }
}
