using System;
using System.Collections.Generic;
using _.Scripts.Utility.GameObject;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

namespace _.Scripts.Gameplay
{
    [CreateAssetMenu(menuName = "Scripts/Gameplay/Plane Manage")]
    public class SessionPreset : ScriptableObject
    {
        [Serializable]
        public class ArrowNode
        {
            public                      int2      index;
            [SerializeReference] public ArrowNode next;
            [SerializeReference] public ArrowNode previous;

            public float4 GetElementPoints()
            {
                return new float4(index, next?.index ?? float2.zero);
            }
        
            public ArrowElement.ElementType GetElementType()
            {
                return next == null ? ArrowElement.ElementType.Nip : ArrowElement.ElementType.Trail;
            }

            public ArrowElement.ElementDirection GetElementDirection(out bool negate)
            {
                if (next == null)
                {
                    return previous.GetElementDirection(out negate);
                }
                else
                {
                    int2 raw = (int2)math.normalize(next.index - index);
                    return ArrowElement.ToDirection(raw, out negate);
                }
            }

            public List<ArrowNode> Flatten()
            {
                ArrowNode root                     = this;
                while (root.previous != null) root = root.previous;
            
                List<ArrowNode> elements = new List<ArrowNode>();
                while (root != null)
                {
                    elements.Add(root);
                
                    root = root.next;
                }

                return elements;
            }
        }

        [SerializeField, ReadOnly] private ArrowNode[] _values;
        
        public                             ArrowNode[] Values => _values;
    }
}