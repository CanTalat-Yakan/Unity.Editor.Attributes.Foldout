#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public class FoldoutGroup
    {
        public string StateKey;
        public string FullPath;
        public int IndentLevel;
        public bool IsExpanded;
        public List<string> PropertyPaths = new();
        public FoldoutGroup ParentGroup;
        public readonly List<FoldoutGroup> ChildGroups = new();
    }

    public static class FoldoutEditor
    {
        private static Dictionary<string, FoldoutGroup> s_foldoutGroupMap = new();
        private static readonly Dictionary<string, bool> s_foldoutStates = new();

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
            if (s_foldoutGroupMap.TryGetValue(property.propertyPath, out var group))
                if (group.ParentGroup == null)
                    DrawGroupHierarchy(group);

            EditorGUI.indentLevel = 0;
        }

        private static void BuildGroupHierarchy(SerializedObject serializedObject)
        {
            s_foldoutStates.Clear();

            SerializedProperty iterator = serializedObject.GetIterator();
            iterator.NextVisible(true); // Skip script field

            FoldoutGroup currentGroup = null;
            while (iterator.NextVisible(false))
            {
                if (TryGetAttributes<EndFoldoutAttribute>(iterator, out var attributes))
                    foreach (var _ in attributes)
                        currentGroup = currentGroup?.ParentGroup;

                if (TryGetAttribute<FoldoutAttribute>(iterator, out var attribute))
                    currentGroup = CreateOrGetGroup(iterator, attribute);

                currentGroup?.PropertyPaths.Add(iterator.propertyPath);
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
                    newGroup = CreateNewGroup(parentGroup, segment);
                    newGroup.PropertyPaths.Add(property.propertyPath);

                    s_foldoutGroupMap[property.propertyPath] = newGroup;

                    parentGroup?.ChildGroups.Add(newGroup);
                    parentGroup = newGroup;
                }

            return newGroup;
        }

        private static bool FindExistingParentGroup(string segment, ref FoldoutGroup parentGroup)
        {
            foreach (var group in s_foldoutGroupMap.Values)
                if (group.FullPath.EndsWith(segment))
                {
                    parentGroup = group;
                    return true;
                }

            return false;
        }

        private static FoldoutGroup CreateNewGroup(FoldoutGroup parent, string segment)
        {
            var fullPath = parent?.FullPath == null ? segment : $"{parent.FullPath}/{segment}";
            var stateKey = $"{InspectorHook.Target.GetInstanceID()}_{fullPath}";

            if (!s_foldoutStates.ContainsKey(stateKey))
                s_foldoutStates[stateKey] = false;

            return new FoldoutGroup
            {
                StateKey = stateKey,
                FullPath = fullPath,
                IndentLevel = (parent?.IndentLevel ?? 0) + 1,
                ParentGroup = parent,
                IsExpanded = s_foldoutStates[stateKey],
            };
        }

        private static void DrawGroupHierarchy(FoldoutGroup group)
        {
            var parentExpanded = IsParentChainExpanded(group);

            DrawFoldoutToggle(group, parentExpanded);
            DrawGroupContent(group, parentExpanded);

            foreach (var child in group.ChildGroups)
                DrawGroupHierarchy(child);
        }

        private static void DrawFoldoutToggle(FoldoutGroup group, bool parentExpanded)
        {
            if (!parentExpanded)
                return;

            EditorGUI.indentLevel = group.IndentLevel - 1;
            group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.FullPath.Split('/').Last(), true);
            s_foldoutStates[group.StateKey] = group.IsExpanded;
            EditorGUI.indentLevel = group.IndentLevel;
        }

        private static void DrawGroupContent(FoldoutGroup group, bool parentExpanded)
        {
            foreach (var path in group.PropertyPaths)
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
        }

        private static bool IsParentChainExpanded(FoldoutGroup group)
        {
            var current = group.ParentGroup;
            while (current != null)
            {
                if (!current.IsExpanded)
                    return false;
                current = current.ParentGroup;
            }
            return true;
        }

        private static bool TryGetAttributes<T>(SerializedProperty property, out List<T> attributes) where T : class
        {
            var field = GetSerializedFieldInfo(property);
            attributes = field?.GetCustomAttributes(typeof(T), true).Cast<T>().ToList() ?? new List<T>();
            return attributes.Count > 0;
        }

        private static bool TryGetAttribute<T>(SerializedProperty property, out T attribute) where T : class
        {
            attribute = null;
            var field = GetSerializedFieldInfo(property);
            return (attribute = field?.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T) != null;
        }

        private static FieldInfo GetSerializedFieldInfo(SerializedProperty property)
        {
            var targetObject = property.serializedObject.targetObject;
            var pathSegment = property.propertyPath.Split('.');
            var fieldInfo = (FieldInfo)null;
            var currentType = targetObject.GetType();


            foreach (var segment in pathSegment)
            {
                // Skip array data paths
                if (segment.StartsWith("Array.data["))
                {
                    Debug.Log(segment);
                    continue;
                }

                fieldInfo = currentType.GetField(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfo == null)
                    return null;

                currentType = fieldInfo.FieldType;
            }

            return fieldInfo;
        }
    }
}
#endif