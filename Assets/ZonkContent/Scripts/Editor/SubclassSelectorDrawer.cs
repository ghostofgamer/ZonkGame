using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor
{
    /// <summary>
    /// Выпадающий список классов для полей [SerializeReference, SubclassSelector].
    /// Список строится из всех неабстрактных [Serializable]-наследников типа поля с конструктором без параметров.
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public sealed class SubclassSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> Cache = new Dictionary<Type, Type[]>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var baseType = ResolveFieldType(property);
            var types = baseType != null ? TypesOf(baseType) : Array.Empty<Type>();

            var current = property.managedReferenceValue?.GetType();
            var names = new[] { "(нет)" }.Concat(types.Select(t => ObjectNames.NicifyVariableName(t.Name))).ToArray();
            var index = current == null ? 0 : Array.IndexOf(types, current) + 1;

            var header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var popupRect = new Rect(header.x + EditorGUIUtility.labelWidth, header.y, header.width - EditorGUIUtility.labelWidth,
                header.height);

            EditorGUI.BeginChangeCheck();
            var selected = EditorGUI.Popup(popupRect, index, names);
            if (EditorGUI.EndChangeCheck() && selected != index)
            {
                property.managedReferenceValue = selected == 0 ? null : Activator.CreateInstance(types[selected - 1]);
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.PropertyField(position, property, label, true);
        }

        private static Type[] TypesOf(Type baseType)
        {
            if (Cache.TryGetValue(baseType, out var cached))
                return cached;

            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.IsSerializable && t.GetConstructor(Type.EmptyTypes) != null &&
                            !typeof(UnityEngine.Object).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToArray();
            Cache[baseType] = types;
            return types;
        }

        /// <summary>Тип поля или элемента списка из строки managedReferenceFieldTypename: "Assembly Namespace.Type".</summary>
        private static Type ResolveFieldType(SerializedProperty property)
        {
            var typename = property.managedReferenceFieldTypename;
            if (string.IsNullOrEmpty(typename))
                return null;

            var space = typename.IndexOf(' ');
            var assembly = typename.Substring(0, space);
            var type = typename.Substring(space + 1);
            return Type.GetType($"{type}, {assembly}");
        }
    }
}
