using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RacingGame
{
#if UNITY_EDITOR
    [CustomEditor(typeof(Car))]
    public class CarEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            Car car = (Car)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Active Component Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Components are executed in the order listed below (based on Priority).", MessageType.Info);

            ICarComponent[] foundComponents = car.GetComponentsInChildren<ICarComponent>();

            if (foundComponents == null || foundComponents.Length == 0)
            {
                EditorGUILayout.HelpBox("No ICarComponents found on this GameObject or its children!", MessageType.Warning);
                return;
            }

            var sortedList = new System.Collections.Generic.List<ICarComponent>(foundComponents);
            sortedList.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (ICarComponent comp in sortedList)
            {
                DrawComponentRow(comp);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentRow(ICarComponent comp)
        {
            EditorGUILayout.BeginHorizontal();

            string typeName = comp.GetType().Name;
            GUIContent label = new(typeName, "Click to highlight in Hierarchy");

            if (GUILayout.Button(label, EditorStyles.label, GUILayout.Width(180)))
            {
                if (comp is MonoBehaviour mb)
                {
                    EditorGUIUtility.PingObject(mb.gameObject);
                }
            }

            GUI.color = GetPriorityColor(comp.Priority);
            EditorGUILayout.LabelField($"Prio: {comp.Priority}", EditorStyles.miniLabel, GUILayout.Width(60));
            GUI.color = Color.white;

            if (comp is MonoBehaviour mono && mono.gameObject != ((Car)target).gameObject)
            {
                GUI.enabled = false;
                EditorGUILayout.LabelField($"(on {mono.gameObject.name})", EditorStyles.miniLabel);
                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();

            Rect lineRect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.2f));
        }

        private Color GetPriorityColor(int priority)
        {
            if (priority < 0) return new Color(0.6f, 1f, 0.6f);
            return priority > 0 ? new Color(1f, 0.8f, 0.4f) : Color.white;
        }
    }
#endif

}
