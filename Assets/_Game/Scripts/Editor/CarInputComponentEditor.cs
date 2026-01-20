using UnityEngine;
using System.Linq;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RacingGame
{
#if UNITY_EDITOR
    [CustomEditor(typeof(CarInputComponent))]
    public class CarInputComponentEditor : Editor
    {
        private SerializedProperty _carInputsProp;
        private Type[] _implementations;
        private string[] _implementationNames;

        private void OnEnable()
        {
            _carInputsProp = serializedObject.FindProperty("_carInputs");

            _implementations = TypeCache.GetTypesDerivedFrom<ICarInputs>()
                .Where(t => !t.IsInterface && !t.IsAbstract && !t.IsSubclassOf(typeof(MonoBehaviour)))
                .ToArray();

            _implementationNames = _implementations.Select(t => t.Name).Prepend("None").ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            int currentIndex = GetCurrentIndex() + 1;

            EditorGUILayout.Space();

            int newIndex = EditorGUILayout.Popup("Input Method", currentIndex, _implementationNames);

            if (newIndex != currentIndex)
            {
                if (newIndex == 0)
                {
                    _carInputsProp.managedReferenceValue = null;
                }
                else
                {
                    object instance = Activator.CreateInstance(_implementations[newIndex - 1]);
                    _carInputsProp.managedReferenceValue = instance;
                }
            }

            if (_carInputsProp.managedReferenceValue != null)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{_carInputsProp.managedReferenceValue.GetType().Name} Settings", EditorStyles.boldLabel);

                SerializedProperty prop = _carInputsProp.Copy();
                foreach (SerializedProperty child in GetChildren(prop))
                {
                    EditorGUILayout.PropertyField(child, true);
                }
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("Please select an input implementation.", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private int GetCurrentIndex()
        {
            if (_carInputsProp.managedReferenceValue == null) return -1;

            Type currentType = _carInputsProp.managedReferenceValue.GetType();
            return Array.IndexOf(_implementations, currentType);
        }

        private System.Collections.Generic.IEnumerable<SerializedProperty> GetChildren(SerializedProperty property)
        {
            SerializedProperty current = property.Copy();
            SerializedProperty nextSibling = property.Copy();
            nextSibling.NextVisible(false);

            if (current.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(current, nextSibling)) break;
                    yield return current.Copy();
                }
                while (current.NextVisible(false));
            }
        }
    }
#endif

}