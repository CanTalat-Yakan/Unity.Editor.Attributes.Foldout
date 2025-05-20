#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UnityEssentials
{
    public class FoldoutGroup
    {
        public string StateKey;
        public string FullPath;
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
            InspectorHook.AddProcessProperty(OnProcessProperty, -1001);
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
            EditorGUI.indentLevel = property.depth;
            if (s_foldoutGroupMap.TryGetValue(property.propertyPath, out var group))
                if (group.ParentGroup == null)
                    DrawGroupHierarchy(group);
        }

        private static void BuildGroupHierarchy(SerializedObject serializedObject)
        {
            s_foldoutStates.Clear();

            FoldoutGroup currentGroup = null;

            InspectorHookUtilities.IterateProperties((property) =>
            {
                if (InspectorHookUtilities.TryGetAttributes<EndFoldoutAttribute>(property, out var attributes))
                    foreach (var _ in attributes)
                        currentGroup = currentGroup?.ParentGroup;

                if (InspectorHookUtilities.TryGetAttribute<FoldoutAttribute>(property, out var attribute))
                    currentGroup = CreateOrGetGroup(property, attribute);

                currentGroup?.PropertyPaths.Add(property.propertyPath);
            });
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
                ParentGroup = parent,
                IsExpanded = s_foldoutStates[stateKey],
            };
        }

        private static void DrawGroupHierarchy(FoldoutGroup group)
        {
            var parentExpanded = IsParentChainExpanded(group);

            EditorGUI.indentLevel++;

            DrawFoldoutToggle(group, parentExpanded);
            DrawGroupContent(group, parentExpanded);

            foreach (var child in group.ChildGroups)
                DrawGroupHierarchy(child);

            EditorGUI.indentLevel--;
        }

        private static void DrawFoldoutToggle(FoldoutGroup group, bool parentExpanded)
        {
            if (!parentExpanded)
                return;

            EditorGUI.indentLevel--;
            group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.FullPath.Split('/').Last(), true);
            EditorGUI.indentLevel++;

            s_foldoutStates[group.StateKey] = group.IsExpanded;
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
    }
}
#endif