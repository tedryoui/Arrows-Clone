using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _.Scripts.Utility.Structures
{
    [Serializable]
    public class CachedIdentityArrayT<T>
    {
#region Fields

        [SerializeField] private List<IdentityT<T>>    _values;
        private Dictionary<string, T> _cache;

#endregion

#region Properties

        public Dictionary<string, T> Values
        {
            get
            {
                if (_cache == null)
                    Cache();
                
                return _cache;
            }
        }

#endregion
        
        public CachedIdentityArrayT(IEnumerable<IdentityT<T>> values)
        {
            _values = values.ToList();
        }

#region Private Methods

        private void Cache()
        {
            _cache = _values.ToDictionary(
                k => k.Identity,
                v => v.Value
            );
        }

#endregion
    }
}