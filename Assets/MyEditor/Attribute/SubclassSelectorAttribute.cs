using System;
using UnityEngine;

// https://qiita.com/tsukimi_neko/items/7922b2433ed4d8616cce
namespace MyEditor.Attribute
{
    [AttributeUsage(AttributeTargets.Field)]
    public class SubclassSelectorAttribute : PropertyAttribute
    {
        private readonly bool _includeMono;

        public SubclassSelectorAttribute(bool includeMono = false)
        {
            _includeMono = includeMono;
        }

        public bool IsIncludeMono()
        {
            return _includeMono;
        }
    }
}