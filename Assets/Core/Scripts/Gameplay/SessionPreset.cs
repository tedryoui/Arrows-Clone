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
            private const int MaxLinkedNodeIterations = 1024;

            public                      int2      index;
            [SerializeReference] public ArrowNode next;
            [SerializeReference] public ArrowNode previous;

            public float4 GetElementPoints()
            {
                return new float4(index, next?.index ?? float2.zero);
            }
        
            public ArrowElement.ElementType GetElementType()
            {
                return ReferenceEquals(next, null) ? ArrowElement.ElementType.Nip : ArrowElement.ElementType.Trail;
            }

            public ArrowElement.ElementDirection GetElementDirection(out bool negate)
            {
                if (ReferenceEquals(next, null))
                {
                    if (ReferenceEquals(previous, null))
                    {
                        negate = false;
                        return ArrowElement.ElementDirection.Horizontal;
                    }

                    return previous.GetElementDirection(out negate);
                }

                int dx = next.index.x - index.x;
                int dy = next.index.y - index.y;

                if (dx == 0 && dy == 0)
                {
                    negate = false;
                    return ArrowElement.ElementDirection.Horizontal;
                }

                int absX = dx >= 0 ? dx : -dx;
                int absY = dy >= 0 ? dy : -dy;

                if (absX >= absY)
                {
                    negate = dx < 0;
                    return ArrowElement.ElementDirection.Horizontal;
                }

                negate = dy < 0;
                return ArrowElement.ElementDirection.Vertical;
            }

            public List<ArrowNode> Flatten()
            {
                List<ArrowNode> elements = new List<ArrowNode>();
                HashSet<ArrowNode> visited = new HashSet<ArrowNode>();

                ArrowNode root = this;
                while (root.previous != null)
                {
                    if (!visited.Add(root) || elements.Count >= MaxLinkedNodeIterations)
                        return elements;

                    root = root.previous;
                }

                visited.Clear();

                while (root != null)
                {
                    if (!visited.Add(root) || elements.Count >= MaxLinkedNodeIterations)
                        return elements;

                    elements.Add(root);
                    root = root.next;
                }

                return elements;
            }
        }

        [SerializeField, ReadOnly] private ArrowNode[] _values;
        [SerializeField] private int2 _boundsSize;

        public                             ArrowNode[] Values              => _values;
        public                             int2          BoundsSize          => _boundsSize;
    }
}