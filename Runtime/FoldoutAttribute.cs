using System;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Groups serialized fields in the Unity Inspector under a collapsible foldout section.
    /// Use this attribute to start a new foldout group with the specified name.
    /// Fields marked with this attribute will appear inside the foldout until an EndFoldoutAttribute is encountered.
    /// </summary>
    public class FoldoutAttribute : PropertyAttribute
    {
        public string Name { get; private set; }

        public FoldoutAttribute(string name) =>
            Name = name;
    }

    /// <summary>
    /// Marks the end of a foldout group started by FoldoutAttribute in the Unity Inspector.
    /// Fields after this attribute will not be included in the previous foldout group.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class EndFoldoutAttribute : PropertyAttribute { }
}
