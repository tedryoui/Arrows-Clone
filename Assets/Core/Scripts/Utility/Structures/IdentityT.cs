using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace _.Scripts.Utility.Structures
{
    [Serializable] [InlineProperty, HideLabel]
    public class IdentityT<T>
    {
        [SerializeField] private string _identity;
        [SerializeField] private T      _value;

        public string Identity => _identity;
        public T Value => _value;
    }
}