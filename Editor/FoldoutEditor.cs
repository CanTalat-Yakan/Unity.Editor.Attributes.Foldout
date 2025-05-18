#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEssentials
{
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class FoldoutEditor : Editor
    {
        private static Dictionary<string, bool> _foldoutStates = new();

        private class FoldoutGroup
        {
            public string Key;
            public string FullPath;
            public List<SerializedProperty> Properties = new();
            public int IndentLevel;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            // Skip the script field
            iterator.NextVisible(enterChildren);
            enterChildren = false;

            Stack<FoldoutGroup> groupStack = new();
            string targetID = target.GetInstanceID().ToString();

            do
            {
                bool isFoldoutStart = TryGetFoldoutAttribute(iterator, out var foldoutAttribute);
                bool isFoldoutEnd = TryGetEndFoldoutAttribute(iterator);

                if (isFoldoutEnd)
                {
                    if (groupStack.Count > 0)
                    {
                        FoldoutGroup group = groupStack.Pop();
                        RenderGroup(group, targetID);
                    }
                    continue;
                }

                if (isFoldoutStart)
                {
                    string[] pathParts = foldoutAttribute.Name.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
                    string accumulatedPath = "";

                    // Find common ancestry with current stack
                    int commonDepth = 0;
                    FoldoutGroup[] currentStack = groupStack.ToArray();
                    foreach (var part in pathParts)
                    {
                        accumulatedPath += (accumulatedPath == "" ? "" : "/") + part;
                        if (commonDepth < currentStack.Length && currentStack[commonDepth].FullPath == accumulatedPath)
                            commonDepth++;
                        else break;
                    }

                    // Close divergent groups
                    while (groupStack.Count > commonDepth)
                    {
                        FoldoutGroup poppedGroup = groupStack.Pop();
                        RenderGroup(poppedGroup, targetID);
                    }

                    // Create new groups for remaining path parts
                    for (int i = commonDepth; i < pathParts.Length; i++)
                    {
                        accumulatedPath = string.Join("/", pathParts.Take(i + 1));
                        string key = $"{targetID}_{accumulatedPath}";

                        if (!_foldoutStates.ContainsKey(key))
                            _foldoutStates[key] = false;

                        // Only show foldout if all parent groups are expanded
                        bool shouldShow = i == 0 || AreAllParentsExpanded(key, targetID);

                        if (shouldShow)
                            _foldoutStates[key] = EditorGUILayout.Foldout(_foldoutStates[key], pathParts[i]);

                        FoldoutGroup newGroup = new()
                        {
                            Key = key,
                            FullPath = accumulatedPath,
                            IndentLevel = EditorGUI.indentLevel
                        };

                        if (shouldShow) 
                            EditorGUI.indentLevel++;

                        groupStack.Push(newGroup);
                    }

                    if (groupStack.Count > 0)
                        groupStack.Peek().Properties.Add(iterator.Copy());
                }
                else
                {
                    if (groupStack.Count > 0)
                        groupStack.Peek().Properties.Add(iterator.Copy());
                    else EditorGUILayout.PropertyField(iterator, true);
                }
            }
            while (iterator.NextVisible(enterChildren));

            // Render remaining groups
            while (groupStack.Count > 0)
            {
                FoldoutGroup group = groupStack.Pop();
                RenderGroup(group, targetID);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool TryGetFoldoutAttribute(SerializedProperty property, out FoldoutAttribute attribute)
        {
            attribute = null;
            FieldInfo field = GetFieldInfoFromProperty(property);
            if (field == null) 
                return false;

            object[] attributes = field.GetCustomAttributes(typeof(FoldoutAttribute), true);
            if (attributes.Length == 0) 
                return false;

            attribute = (FoldoutAttribute)attributes[0];
            return true;
        }

        private bool TryGetEndFoldoutAttribute(SerializedProperty property)
        {
            FieldInfo field = GetFieldInfoFromProperty(property);
            if (field == null) 
                return false;

            object[] attributes = field.GetCustomAttributes(typeof(EndFoldoutAttribute), true);
            return attributes.Length > 0;
        }

        private FieldInfo GetFieldInfoFromProperty(SerializedProperty property)
        {
            string[] paths = property.propertyPath.Split('.');
            System.Type type = serializedObject.targetObject.GetType();

            FieldInfo fieldInfo = null;
            foreach (string path in paths)
            {
                fieldInfo = type.GetField(path, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo == null) 
                    return null;

                // Handle nested serialized classes
                if (path == "Array" && paths.Length > 1 && paths[0].Contains("["))
                    // Handle array elements
                    type = fieldInfo.FieldType.GetElementType();
                else type = fieldInfo.FieldType;
            }

            return fieldInfo;
        }

        private bool AreAllParentsExpanded(string key, string targetID)
        {
            string[] keyParts = key.Split(new[] { '_' }, 2);
            string fullPath = keyParts[1];
            string[] pathSegments = fullPath.Split('/');

            for (int i = 0; i < pathSegments.Length - 1; i++)
            {
                string parentPath = string.Join("/", pathSegments.Take(i + 1));
                string parentKey = $"{targetID}_{parentPath}";

                if (!_foldoutStates.TryGetValue(parentKey, out bool parentState) || !parentState)
                    return false;
            }
            return true;
        }

        private void RenderGroup(FoldoutGroup group, string targetID)
        {
            if (AreAllParentsExpanded(group.Key, targetID)
                && _foldoutStates.TryGetValue(group.Key, out bool state)
                && state)
            {
                EditorGUI.indentLevel = group.IndentLevel + 1;
                foreach (var property in group.Properties)
                    EditorGUILayout.PropertyField(property, true);
            }
            EditorGUI.indentLevel = group.IndentLevel;
        }
    }
}
#endif