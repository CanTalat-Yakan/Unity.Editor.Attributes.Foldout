using UnityEngine;

namespace UnityEssentials
{
    public class FoldoutAttribute : PropertyAttribute
    {
        public string Name { get; private set; }

        public FoldoutAttribute(string name) =>
            Name = name;
    }

    public class EndFoldoutAttribute : PropertyAttribute { }
}
