using System;
using UnityEngine;

namespace UnityEssentials
{
    public class FoldoutAttribute : PropertyAttribute
    {
        public string Name { get; private set; }

        public FoldoutAttribute(string name) =>
            Name = name;
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class EndFoldoutAttribute : PropertyAttribute { }
}
