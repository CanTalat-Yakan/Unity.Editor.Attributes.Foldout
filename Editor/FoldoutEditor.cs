#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UnityEssentials
{
    /// <summary>
    /// Represents a hierarchical group of properties with an expandable or collapsible state.
    /// </summary>
    /// <remarks>This class is designed to manage a collection of properties and nested groups, allowing for 
    /// hierarchical organization and state tracking. Each group can have a parent group and multiple  child groups,
    /// enabling the creation of complex nested structures.</remarks>
    public class FoldoutGroup
    {
        public string StateKey;
        public string FullPath;
        public bool IsExpanded;
        public List<string> PropertyPaths = new();
        public FoldoutGroup ParentGroup;
        public readonly List<FoldoutGroup> ChildGroups = new();
    }

    /// <summary>
    /// Provides functionality for managing and rendering foldout groups in the Unity Editor's inspector.
    /// </summary>
    /// <remarks>The <see cref="FoldoutEditor"/> class is designed to enhance the Unity Editor by organizing
    /// serialized properties into hierarchical foldout groups. It integrates with the <c>InspectorHook</c> system to
    /// process and display properties dynamically based on custom attributes such as <c>FoldoutAttribute</c> and
    /// <c>EndFoldoutAttribute</c>.</remarks>
    public static class FoldoutEditor
    {
        private static Dictionary<string, FoldoutGroup> s_foldoutGroupMap = new();
        private static readonly Dictionary<string, bool> s_foldoutStates = new();

        [InitializeOnLoadMethod]
        public static void Initialization()
        {
            InspectorHook.AddInitialization(OnInitialize);
            InspectorHook.AddProcessProperty(OnProcessProperty, 1000);
        }

        /// <summary>
        /// Initializes the system by building the group hierarchy for the serialized object.
        /// </summary>
        /// <remarks>This method retrieves the serialized object from the <see cref="InspectorHook"/> and,
        /// if it is not null,  constructs the group hierarchy. If the serialized object is null, the method exits
        /// without performing any action.</remarks>
        public static void OnInitialize()
        {
            var serializedObject = InspectorHook.SerializedObject;
            if (serializedObject == null)
                return;

            BuildGroupHierarchy(serializedObject);
        }

        /// <summary>
        /// Processes a serialized property and adjusts the editor's indentation level based on the property's depth.
        /// </summary>
        /// <remarks>If the property is associated with a foldout group in the internal foldout group map,
        /// and the group has no parent, the method draws the hierarchy for that group.</remarks>
        /// <param name="property">The serialized property to process. Must not be null.</param>
        private static void OnProcessProperty(SerializedProperty property)
        {
            EditorGUI.indentLevel = property.depth;
            if (s_foldoutGroupMap.TryGetValue(property.propertyPath, out var group))
                if (group.ParentGroup == null)
                    DrawGroupHierarchy(group);
        }

        /// <summary>
        /// Builds a hierarchy of foldout groups based on the properties and their associated attributes in the given
        /// serialized object.
        /// </summary>
        /// <remarks>This method iterates through the properties of the provided <paramref
        /// name="serializedObject"/> and organizes them into a hierarchy of foldout groups. Properties with <see
        /// cref="FoldoutAttribute"/> are added to the corresponding group, while properties with <see
        /// cref="EndFoldoutAttribute"/> signal the end of a group. The resulting hierarchy is stored in an internal
        /// structure for further use.</remarks>
        /// <param name="serializedObject">The serialized object containing the properties to process.</param>
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

        /// <summary>
        /// Creates a new foldout group or retrieves an existing one based on the specified property and attribute.
        /// </summary>
        /// <remarks>The method processes the hierarchical structure defined by the <paramref
        /// name="attribute"/> and ensures that  the foldout group is properly nested within its parent groups. If a
        /// group already exists for a given segment,  it is reused; otherwise, a new group is created.</remarks>
        /// <param name="property">The serialized property associated with the foldout group.</param>
        /// <param name="attribute">The <see cref="FoldoutAttribute"/> that defines the hierarchy and naming of the foldout group.</param>
        /// <returns>A <see cref="FoldoutGroup"/> instance representing the created or retrieved group.  If the group does not
        /// exist, a new one is created and added to the hierarchy.</returns>
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

        /// <summary>
        /// Searches for an existing parent group whose full path ends with the specified segment.
        /// </summary>
        /// <param name="segment">The segment to match against the end of the full path of each group.</param>
        /// <param name="parentGroup">When the method returns <see langword="true"/>, contains the matching parent group whose full path ends with
        /// the specified segment. If no match is found, the value remains unchanged.</param>
        /// <returns><see langword="true"/> if a matching parent group is found; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>
        /// Creates a new <see cref="FoldoutGroup"/> instance with the specified parent group and segment name.
        /// </summary>
        /// <remarks>The group's full path is constructed by combining the parent's full path (if any)
        /// with the specified segment. The group's expansion state is determined by a cached state key, which is
        /// initialized to <see langword="false"/> if not already present.</remarks>
        /// <param name="parent">The parent <see cref="FoldoutGroup"/> to which the new group belongs. Can be <see langword="null"/> if the
        /// group has no parent.</param>
        /// <param name="segment">The name of the segment representing this group. Cannot be <see langword="null"/> or empty.</param>
        /// <returns>A new <see cref="FoldoutGroup"/> instance with its state and hierarchy initialized.</returns>
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

        /// <summary>
        /// Recursively renders the hierarchy of foldout groups in the editor UI.
        /// </summary>
        /// <remarks>This method adjusts the indentation level for each group and ensures that child
        /// groups are rendered only if their parent groups are expanded. It is intended for use in custom editor UI
        /// rendering.</remarks>
        /// <param name="group">The root <see cref="FoldoutGroup"/> to render, including its child groups.</param>
        private static void DrawGroupHierarchy(FoldoutGroup group)
        {
            var parentExpanded = IsParentChainExpanded(group);

            EditorGUI.indentLevel++;
            {
                DrawFoldoutToggle(group, parentExpanded);
                DrawGroupContent(group, parentExpanded);

                foreach (var child in group.ChildGroups)
                    DrawGroupHierarchy(child);
            }
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Draws a foldout toggle for the specified <see cref="FoldoutGroup"/> in the Unity Editor.
        /// </summary>
        /// <remarks>This method adjusts the indentation level in the Unity Editor and updates the foldout
        /// state for the specified group. The foldout label is derived from the last segment of the group's full
        /// path.</remarks>
        /// <param name="group">The <see cref="FoldoutGroup"/> to render the foldout toggle for.</param>
        /// <param name="parentExpanded">A value indicating whether the parent group is expanded. If <see langword="false"/>, the foldout toggle will
        /// not be drawn.</param>
        private static void DrawFoldoutToggle(FoldoutGroup group, bool parentExpanded)
        {
            if (!parentExpanded)
                return;

            EditorGUI.indentLevel--;
            {
                group.IsExpanded = EditorGUILayout.Foldout(group.IsExpanded, group.FullPath.Split('/').Last(), true);
            }
            EditorGUI.indentLevel++;

            s_foldoutStates[group.StateKey] = group.IsExpanded;
        }

        /// <summary>
        /// Renders the content of a <see cref="FoldoutGroup"/> in the inspector, based on its expansion state and
        /// parent visibility.
        /// </summary>
        /// <remarks>This method iterates through the property paths defined in the <paramref
        /// name="group"/> and determines whether  each property should be drawn or marked as handled, depending on the
        /// expansion states of the group and its parent.</remarks>
        /// <param name="group">The <see cref="FoldoutGroup"/> whose properties are to be rendered.</param>
        /// <param name="parentExpanded">A value indicating whether the parent group is expanded.  If <see langword="true"/>, the group's content
        /// will be considered for rendering based on its own expansion state.</param>
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

        /// <summary>
        /// Determines whether all parent groups in the hierarchy of the specified <see cref="FoldoutGroup"/> are
        /// expanded.
        /// </summary>
        /// <param name="group">The <see cref="FoldoutGroup"/> whose parent chain is to be checked.</param>
        /// <returns><see langword="true"/> if all parent groups in the hierarchy are expanded; otherwise, <see
        /// langword="false"/>.</returns>
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