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
        public bool IsExpanded;
        public List<string> ContentPaths = new();
        public FoldoutGroup Parent;
        public readonly List<FoldoutGroup> Children = new();
    }

    public static class FoldoutEditor
    {
        private static readonly Dictionary<string, bool> _foldoutStates = new();
        private static Dictionary<string, FoldoutGroup> _groupMap = new();

        [InitializeOnLoadMethod]
        public static void Initialization()
        {
            InspectorHook.AddInitialization(OnInitialize);
            InspectorHook.AddProcessProperty(OnProcessProperty);
        }

        public static void OnInitialize()
        {
            var serializedObject = InspectorHook.SerializedObject;
            if (serializedObject == null)
                return;

            BuildGroupHierarchy(serializedObject);
        }

        private static void OnProcessProperty(SerializedProperty property)
        {
            if (_groupMap.TryGetValue(property.propertyPath, out var group))
                if (group.Parent == null)
                    RenderFoldoutGroup(group);

            EditorGUI.indentLevel = 0;
        }

        private static void BuildGroupHierarchy(SerializedObject serializedObject)
        {
            FoldoutGroup currentGroup = null;

            _groupMap.Clear();

            SerializedProperty iterator = serializedObject.GetIterator();
            iterator.NextVisible(true); // Skip script field

            while (iterator.NextVisible(false))
            {
                if (TryGetAttributes<EndFoldoutAttribute>(iterator, out var attributes))
                    foreach (var _ in attributes)
                        currentGroup = currentGroup?.Parent;

                if (TryGetAttribute<FoldoutAttribute>(iterator, out var attribute))
                    currentGroup = CreateOrGetGroup(iterator, attribute);

                currentGroup?.ContentPaths.Add(iterator.propertyPath);
            }
        }

        private static FoldoutGroup CreateOrGetGroup(SerializedProperty property, FoldoutAttribute attribute)
        {
            FoldoutGroup newGroup = null;
            FoldoutGroup parentGroup = null;

            var pathSegments = attribute.Name.Split('/');
            foreach (var segment in pathSegments)
                if (!FindExistingParentGroup(segment, ref parentGroup))
                {
                    newGroup = CreateGroup(parentGroup, segment);
                    newGroup.ContentPaths.Add(property.propertyPath);

                    _groupMap[property.propertyPath] = newGroup;

                    parentGroup?.Children.Add(newGroup);
                    parentGroup = newGroup;
                }

            return newGroup;
        }

        private static bool FindExistingParentGroup(string segment, ref FoldoutGroup parentGroup)
        {
            foreach (var group in _groupMap.Values)
                if (group.Path.EndsWith(segment))
                {
                    parentGroup = group;
                    return true;
                }

            return false;
        }

        private static FoldoutGroup CreateGroup(FoldoutGroup parent, string segment)
        {
            var path = parent?.Path == null ? segment : $"{parent.Path}/{segment}";
            var key = $"{InspectorHook.Target.GetInstanceID()}_{path}";

            if (!_foldoutStates.ContainsKey(key))
                _foldoutStates[key] = false;

            return new FoldoutGroup
            {
                Key = key,
                Path = path,
                Indent = parent?.Indent + 1 ?? 1,
                Parent = parent,
                IsExpanded = _foldoutStates[key],
            };
        }

        private static void RenderFoldoutGroup(FoldoutGroup group)
        {
            var parentExpanded = ShouldShowGroup(group);
            if (parentExpanded)
            {
                EditorGUI.indentLevel = group.Indent - 1;
                group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.Path.Split('/').Last());
                _foldoutStates[group.Key] = group.IsExpanded;
                EditorGUI.indentLevel = group.Indent;
            }

            foreach (var path in group.ContentPaths)
            {
                var contentProperty = InspectorHook.SerializedObject.FindProperty(path);
                if (contentProperty == null)
                    continue;

                if (InspectorHook.IsPropertyHandled(path))
                    continue;

                if (group.IsExpanded && parentExpanded)
                    InspectorHook.DrawProperty(contentProperty, true);
                else InspectorHook.MarkPropertyAsHandled(contentProperty.propertyPath);
            }

            foreach (var child in group.Children)
                RenderFoldoutGroup(child);
        }

        private static bool ShouldShowGroup(FoldoutGroup group)
        {
            var current = group.Parent;
            while (current != null)
            {
                if (!current.IsExpanded)
                    return false;
                current = current.Parent;
            }
            return true;
        }

        private static bool TryGetAttributes<T>(SerializedProperty property, out List<T> attributes) where T : class
        {
            var field = GetFieldInfo(property);
            attributes = field?.GetCustomAttributes(typeof(T), true).Cast<T>().ToList() ?? new List<T>();
            return attributes.Count > 0;
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
                if (path.StartsWith("Array.data["))
                    continue;

                field = type.GetField(path, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                    return null;

                type = field.FieldType;
            }

            return field;
        }
    }
}
#endif