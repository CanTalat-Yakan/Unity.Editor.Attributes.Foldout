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
        private static readonly Dictionary<string, bool> _foldoutStates = new();
        private readonly Stack<FoldoutGroup> _groupStack = new();

        private class FoldoutGroup
        {
            public string Key;
            public string Path;
            public int Indent;
            public readonly List<SerializedProperty> Properties = new();
            public bool IsExpanded;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var iterator = serializedObject.GetIterator();

            // Skip script field
            iterator.NextVisible(true);

            while (iterator.NextVisible(false))
                ProcessProperty(iterator);

            RenderRemainingGroups();
            serializedObject.ApplyModifiedProperties();
        }

        private void ProcessProperty(SerializedProperty property)
        {
            if (TryGetAttribute<EndFoldoutAttribute>(property, out _))
            {
                PopAndRenderGroup();
                return;
            }

            if (TryGetAttribute<FoldoutAttribute>(property, out var attribute))
            {
                ProcessFoldoutStart(property, attribute);
                return;
            }

            AddToCurrentGroupOrDraw(property);
        }

        private void ProcessFoldoutStart(SerializedProperty property, FoldoutAttribute attribute)
        {
            var pathParts = attribute.Name.Split('/');
            var commonDepth = GetCommonDepth(pathParts);

            PopDivergentGroups(commonDepth);
            CreateNewGroups(pathParts, commonDepth);

            _groupStack.Peek().Properties.Add(property.Copy());
        }

        private int GetCommonDepth(string[] pathParts)
        {
            int depth = 0;
            string currentPath = "";

            foreach (var part in pathParts)
            {
                currentPath += (currentPath == "" ? "" : "/") + part;
                if (depth < _groupStack.Count && _groupStack.ElementAt(depth).Path == currentPath)
                    depth++;
                else break;
            }
            return depth;
        }

        private void PopDivergentGroups(int commonDepth)
        {
            while (_groupStack.Count > commonDepth)
                PopAndRenderGroup();
        }

        private void CreateNewGroups(string[] pathParts, int startDepth)
        {
            string currentPath = "";
            for (int i = 0; i < startDepth; i++)
                currentPath += (currentPath == "" ? "" : "/") + pathParts[i];

            for (int i = startDepth; i < pathParts.Length; i++)
            {
                currentPath += (currentPath == "" ? "" : "/") + pathParts[i];
                var group = CreateGroup(currentPath);

                if (ShouldShowGroup(group.Key))
                {
                    EditorGUI.indentLevel = group.Indent;
                    group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, pathParts[i]);
                    _foldoutStates[group.Key] = group.IsExpanded;
                }

                _groupStack.Push(group);

                if (group.IsExpanded)
                    EditorGUI.indentLevel++;
            }
        }

        private FoldoutGroup CreateGroup(string path)
        {
            var key = $"{target.GetInstanceID()}_{path}";
            if (!_foldoutStates.TryGetValue(key, out bool isExpanded))
                _foldoutStates[key] = false;

            return new FoldoutGroup
            {
                Key = key,
                Path = path,
                Indent = EditorGUI.indentLevel,
                IsExpanded = isExpanded
            };
        }

        private bool ShouldShowGroup(string key)
        {
            string[] parts = key.Split('_')[1].Split('/');
            string currentPath = "";

            for (int i = 0; i < parts.Length - 1; i++)
            {
                currentPath += (currentPath == "" ? "" : "/") + parts[i];
                string parentKey = $"{target.GetInstanceID()}_{currentPath}";

                if (!_foldoutStates.TryGetValue(parentKey, out bool isExpanded) || !isExpanded)
                    return false;
            }
            return true;
        }

        private void AddToCurrentGroupOrDraw(SerializedProperty property)
        {
            if (_groupStack.Count > 0)
                _groupStack.Peek().Properties.Add(property.Copy());
            else EditorGUILayout.PropertyField(property, true);
        }

        private void PopAndRenderGroup()
        {
            if (_groupStack.Count == 0) 
                return;

            var group = _groupStack.Pop();

            if (!ShouldShowGroup(group.Key))
                return;

            EditorGUI.indentLevel = group.Indent;
            if (group.IsExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (var property in group.Properties)
                    EditorGUILayout.PropertyField(property, true);
            }
            EditorGUI.indentLevel = group.Indent;
        }

        private void RenderRemainingGroups()
        {
            while (_groupStack.Count > 0)
                PopAndRenderGroup();
        }

        private static bool TryGetAttribute<T>(SerializedProperty property, out T attribute) where T : class
        {
            attribute = null;
            var field = GetFieldInfo(property);
            return (attribute = field?.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T) != null;
        }

        private static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            System.Type type = property.serializedObject.targetObject.GetType();
            FieldInfo field = null;

            foreach (var path in property.propertyPath.Split('.'))
            {
                field = type.GetField(path, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field == null) break;
                type = field.FieldType;
            }
            return field;
        }
    }
}
#endif