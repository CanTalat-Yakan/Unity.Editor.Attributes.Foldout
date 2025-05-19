#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace UnityEssentials
{
    public class FoldoutGroup
    {
        public string Key;
        public string Path;
        public int Indent;
        public readonly List<SerializedProperty> Properties = new();
        public bool IsExpanded;
    }

    public static class FoldoutEditor
    {
        private static readonly Dictionary<string, bool> s_foldoutStates = new();
        private static readonly Stack<FoldoutGroup> s_groupStack = new();

        [InitializeOnLoadMethod]
        public static void Initialization() => InspectorHook.Add(OnInspectorGUI);
        public static void OnInspectorGUI()
        {
            var serializedObject = InspectorHook.SerializedObject;

            serializedObject.Update();
            var iterator = serializedObject.GetIterator();
            iterator.NextVisible(true); // Skip script field

            while (iterator.NextVisible(false))
                ProcessProperty(iterator);

            RenderRemainingGroups();
            serializedObject.ApplyModifiedProperties();
        }

        private static void ProcessProperty(SerializedProperty property)
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

            if (property.hasVisibleChildren && !IsPrimitive(property))
            {
                var depth = property.depth;
                var child = property.Copy();
                if (child.NextVisible(true))
                {
                    do { ProcessProperty(child); }
                    while (child.NextVisible(false) && child.depth > depth);
                }
            }

            AddToCurrentGroupOrDraw(property);
        }

        private static void ProcessFoldoutStart(SerializedProperty property, FoldoutAttribute attribute)
        {
            var pathParts = attribute.Name.Split('/');
            var commonDepth = GetCommonDepth(pathParts);

            PopDivergentGroups(commonDepth);
            CreateNewGroups(pathParts, commonDepth);

            s_groupStack.Peek().Properties.Add(property.Copy());
        }

        private static int GetCommonDepth(string[] pathParts)
        {
            int depth = 0;
            string currentPath = "";

            foreach (var part in pathParts)
            {
                currentPath += (currentPath == "" ? "" : "/") + part;
                if (depth < s_groupStack.Count && s_groupStack.ElementAt(depth).Path == currentPath)
                    depth++;
                else break;
            }
            return depth;
        }

        private static void PopDivergentGroups(int commonDepth)
        {
            while (s_groupStack.Count > commonDepth)
                PopAndRenderGroup();
        }

        private static void CreateNewGroups(string[] pathParts, int startDepth)
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
                    s_foldoutStates[group.Key] = group.IsExpanded;
                }

                s_groupStack.Push(group);

                if (group.IsExpanded)
                    EditorGUI.indentLevel++;
            }
        }

        private static FoldoutGroup CreateGroup(string path)
        {
            var key = $"{InspectorHook.Target.GetInstanceID()}_{path}";
            if (!s_foldoutStates.TryGetValue(key, out bool isExpanded))
                s_foldoutStates[key] = false;

            return new FoldoutGroup
            {
                Key = key,
                Path = path,
                Indent = EditorGUI.indentLevel,
                IsExpanded = isExpanded
            };
        }

        private static bool ShouldShowGroup(string key)
        {
            string[] parts = key.Split('_')[1].Split('/');
            string currentPath = "";

            for (int i = 0; i < parts.Length - 1; i++)
            {
                currentPath += (currentPath == "" ? "" : "/") + parts[i];
                string parentKey = $"{InspectorHook.Target.GetInstanceID()}_{currentPath}";

                if (!s_foldoutStates.TryGetValue(parentKey, out bool isExpanded) || !isExpanded)
                    return false;
            }
            return true;
        }

        private static void AddToCurrentGroupOrDraw(SerializedProperty property)
        {
            if (s_groupStack.Count > 0)
                s_groupStack.Peek().Properties.Add(property.Copy());
            else EditorGUILayout.PropertyField(property, true);
        }

        private static void PopAndRenderGroup()
        {
            if (s_groupStack.Count == 0)
                return;

            var group = s_groupStack.Pop();

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

        private static void RenderRemainingGroups()
        {
            while (s_groupStack.Count > 0)
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
            object obj = property.serializedObject.targetObject;
            string[] paths = property.propertyPath.Split('.');
            FieldInfo field = null;
            System.Type type = obj.GetType();

            foreach (var path in paths)
            {
                // Handle array elements (e.g., "Array.data[0]")
                if (path.StartsWith("Array.data["))
                    continue;

                field = type.GetField(path, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) return null;
                type = field.FieldType;
            }

            return field;
        }

        private static bool IsPrimitive(SerializedProperty property) =>
            property.propertyType != SerializedPropertyType.Generic;
    }
}
#endif