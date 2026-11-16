using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MissionProgressData))]
public sealed class MissionProgressDataEditor : Editor
{
    private SerializedProperty goalObject;
    private SerializedProperty missionProgressUnits;

    private void OnEnable()
    {
        goalObject = serializedObject.FindProperty("goalObject");
        missionProgressUnits = serializedObject.FindProperty("missionProgressUnits");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        MissionProgressData data = (MissionProgressData)target;
        List<GameObject> childObjects = CollectChildObjects(data.transform);
        GameObject[] options = new GameObject[childObjects.Count + 1];
        string[] labels = new string[childObjects.Count + 1];
        options[0] = null;
        labels[0] = "None";

        for (int i = 0; i < childObjects.Count; i++)
        {
            options[i + 1] = childObjects[i];
            labels[i + 1] = GetHierarchyPath(childObjects[i].transform, data.transform);
        }

        EditorGUILayout.HelpBox(
            "Goal, Trigger, Progress, Target는 이 컴포넌트가 붙은 GameObject의 자녀 오브젝트만 선택할 수 있습니다.",
            MessageType.Info);

        DrawChildObjectPopup("Goal Object", goalObject, options, labels);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mission Progress Units", EditorStyles.boldLabel);
        missionProgressUnits.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Size", missionProgressUnits.arraySize));

        for (int i = 0; i < missionProgressUnits.arraySize; i++)
        {
            SerializedProperty unit = missionProgressUnits.GetArrayElementAtIndex(i);
            SerializedProperty triggerObject = unit.FindPropertyRelative("triggerObject");
            SerializedProperty progressObjects = unit.FindPropertyRelative("progressObjects");
            SerializedProperty triggerTargetObject = unit.FindPropertyRelative("triggerTargetObject");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Unit {i + 1}", EditorStyles.boldLabel);
            DrawChildObjectPopup("Trigger Object", triggerObject, options, labels);

            progressObjects.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Progress Objects", progressObjects.arraySize));
            EditorGUI.indentLevel++;
            for (int j = 0; j < progressObjects.arraySize; j++)
            {
                DrawChildObjectPopup(
                    $"Progress {j + 1}",
                    progressObjects.GetArrayElementAtIndex(j),
                    options,
                    labels);
            }
            EditorGUI.indentLevel--;

            DrawChildObjectPopup("Trigger Target Object", triggerTargetObject, options, labels);
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawChildObjectPopup(
        string label,
        SerializedProperty property,
        GameObject[] options,
        string[] labels)
    {
        int selectedIndex = 0;
        if (property.objectReferenceValue is GameObject selectedObject)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == selectedObject)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        int nextIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
        property.objectReferenceValue = options[nextIndex];
    }

    private static List<GameObject> CollectChildObjects(Transform root)
    {
        List<GameObject> result = new List<GameObject>();
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i] != root)
            {
                result.Add(descendants[i].gameObject);
            }
        }

        return result;
    }

    private static string GetHierarchyPath(Transform target, Transform root)
    {
        List<string> names = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
