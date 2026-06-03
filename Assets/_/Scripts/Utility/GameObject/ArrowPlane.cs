using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace _.Scripts.Utility.GameObject
{
    [ExecuteAlways]
    public partial class ArrowPlane : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _meshRenderer;
        [SerializeField] private MeshFilter   _meshFilter;
        [SerializeField] private float2       _sizeDelta;
        [SerializeField] private string       _seed;
        
        [SerializeField, ReadOnly] private float3[] _corners = new  float3[4]
        {
            new float3(-0.5f, -0.5f, 0.0f),
            new float3(-0.5f, 0.5f, 0.0f),
            new float3(0.5f, 0.5f, 0.0f),
            new float3(0.5f, -0.5f, 0.0f)
        };
        
        [SerializeField] private List<Arrow> _arrows = new ()
        {
            new Arrow(Arrow.ElementType.Nip,   Arrow.ElementDirection.Horizontal, true, new float4(0, 0, 0, 0)),
            new Arrow(Arrow.ElementType.Trail, Arrow.ElementDirection.Horizontal, true, new float4(0, 0, 1, 0)),
            new Arrow(Arrow.ElementType.Trail, Arrow.ElementDirection.Vertical,   true, new float4(1, 0, 1, 1)),
            new Arrow(Arrow.ElementType.Trail, Arrow.ElementDirection.Vertical,   true, new float4(1, 1, 2, 1))
        };
        
        private static readonly float2[] Directions = new[]
        {
            new float2(1f,  0f),
            new float2(-1f, 0f),
            new float2(0f,  1f),
            new float2(0f,  -1f)
        };

        
        private ComputeBuffer            _computeBuffer;
        private Unity.Mathematics.Random _random;

        private void Start() 
        {
            RefreshRenderer();

            _random = new Unity.Mathematics.Random();
            _random.InitState(1);
            GenerateArrows();
        }

        [Button]
        private void GenerateArrows()
        {
            var quals = _sizeDelta % 2 == 0;
            var set   = FillField(
                -(int2)math.floor(_sizeDelta * 0.5f) + (quals.x ? 1 : 0), 
                 (int2)math.ceil (_sizeDelta * 0.5f)
            );

            _arrows = set.Where(x => x.Count > 1).SelectMany(x =>
            {
                var head  = x.Last();
                var others = x.SkipLast(1).ToArray();

                var arrows = new List<Arrow>()
                {
                    new Arrow(Arrow.ElementType.Nip, ToDirection(head - others.Last(), out var negate), negate,
                        new float4(head, 0.0f))
                };

                var previous = head;
                
                foreach (var other in others.Reverse())
                {
                    arrows.Add(
                        new Arrow(Arrow.ElementType.Trail, ToDirection(previous - other, out var b), b, new float4(previous, other)));

                    previous = other;
                }

                return arrows;
            }).ToList();
        }
        
        private List<HashSet<float2>> FillField(int2 minBounds, int2 maxBounds)
        {
            HashSet<float2>       availableNodes = CreateAvailableNodes(minBounds, maxBounds);
            List<HashSet<float2>> allLines       = new List<HashSet<float2>>();

            while (availableNodes.Count > 0)
            {
                HashSet<float2> currentLine = StartNewLine(availableNodes);
                if (currentLine == null) break;

                allLines.Add(currentLine);

                while (ExtendLine(currentLine, availableNodes) &&
                       _random.NextInt(0, 100) < 75 &&
                       currentLine.Count <= 6)
                { }
            }

            return allLines;
        }
        
        private HashSet<float2> StartNewLine(HashSet<float2> availableNodes)
        {
            if (availableNodes.Count == 0) return null;

            int    randomIndex = _random.NextInt(0, availableNodes.Count);
            float2 startNode   = availableNodes.Skip(randomIndex).First();

            availableNodes.Remove(startNode);

            HashSet<float2> newLine = new HashSet<float2>();
            newLine.Add(startNode);
        
            return newLine;
        }
        
        private bool ExtendLine(HashSet<float2> line, HashSet<float2> availableNodes)
        {
            if (line.Count == 0) return false;

            float2       currentHead = line.Last();
            List<float2> validMoves  = new List<float2>();

            foreach (var dir in Directions)
            {
                float2 nextPos = currentHead + dir;
            
                if (availableNodes.Contains(nextPos))
                {
                    validMoves.Add(nextPos);
                }
            }

            if (validMoves.Count == 0)
            {
                return false; 
            }

            float2 chosenMove = validMoves[_random.NextInt(0, validMoves.Count)];

            availableNodes.Remove(chosenMove);

            line.Add(chosenMove);

            return true;
        }
        
        private HashSet<float2> CreateAvailableNodes(int2 minBounds, int2 maxBounds)
        {
            var availableNodes = new HashSet<float2>();
            for (int x = minBounds.x; x < maxBounds.x; x++)
            {
                for (int y = minBounds.y; y < maxBounds.y; y++)
                {
                    availableNodes.Add(new float2(x, y));
                }
            }
            return availableNodes;
        }


        private void Update()
        {
// #if UNITY_EDITOR
//             if (EditorApplication.isPlaying)
// #endif
//                 UpdateArrow();
            UpdateMaterial();
        }

        [SerializeField] private float2 _previousArrowPosition;

        private void UpdateArrow()
        {
            if (_arrows == null || _arrows.Count == 0) 
                return;
            
            var arrow     = _arrows[0];
            var direction = FromDirection(arrow.elementDirection, arrow.elementDirectionNegate == 1);

            var arrowPosition = arrow.elementPoints.xy;

            if (math.distance(arrowPosition, new float2(-5, 0)) >= 0.0f) 
            {
                arrowPosition += direction * Time.deltaTime * 3;
                
                arrow.elementPoints = new float4(arrowPosition, 0.0f);
                _arrows[0]          = arrow;

                var trail = _arrows[1];
                trail.elementPoints = new float4(trail.elementPoints.xy, arrowPosition);

                _arrows[1] = trail;
            }

            if (math.distance(arrowPosition, _previousArrowPosition) >= 1.0f)
            {
                var trail = new Arrow(Arrow.ElementType.Trail, arrow.elementDirection, arrow.elementDirectionNegate == 1, new float4(arrowPosition, arrowPosition));
                
                _arrows.Insert(1, trail);

                _previousArrowPosition = arrowPosition;
            }

            if (_arrows.Count > 3)
            {
                var lastTrail = _arrows[^1];
                
                var current   = lastTrail.elementPoints.xy;
                var currentDirection = FromDirection(lastTrail.elementDirection, lastTrail.elementDirectionNegate == 1);

                current += currentDirection * Time.deltaTime * 3;
                
                var target  = lastTrail.elementPoints.zw;

                lastTrail.elementPoints = new float4(current, target);
                _arrows[^1]             = lastTrail;

                if (math.distance(target, current) <= 0.05f)
                {
                    _arrows.RemoveAt(_arrows.Count - 1);
                }
            }
        }

        float2 FromDirection(Arrow.ElementDirection direction, bool negate)
        {
            var fDir = new float2(0, 0);

            if (direction is Arrow.ElementDirection.Horizontal)
            {
                if (!negate)
                    fDir.x  = 1;
                else fDir.x = -1;
            }
            else
            {
                if (!negate)
                    fDir.y  = 1;
                else fDir.y = -1;
            }

            return fDir;
        }

        Arrow.ElementDirection ToDirection(float2 dir, out bool negate)
        {
            var rDir = math.normalize(dir);
            
            if (rDir is { x: 1, y: 0 })
            {
                negate = false;
                return Arrow.ElementDirection.Horizontal;
            }

            if (rDir is { x: -1, y: 0 })
            {
                negate = true;
                return Arrow.ElementDirection.Horizontal;
            }

            if (rDir is { x: 0, y: 1 })
            {
                negate = false;
                return Arrow.ElementDirection.Vertical;
            }

            if (rDir is { x: 0, y: -1 })
            {
                negate = true;
                return Arrow.ElementDirection.Vertical;
            }
            
            throw new Exception("Invalid direction");
        }

        private void UpdateMaterial()
        {
            if (_computeBuffer != null && _computeBuffer.IsValid())
                _computeBuffer.Dispose();

            if (_arrows == null || _arrows.Count == 0)
                return;
            
            _computeBuffer = new ComputeBuffer(_arrows.Count, 
                sizeof(Arrow.ElementType) + 
                sizeof(Arrow.ElementDirection) +
                sizeof(int) +
                sizeof(float) * 4);
            _computeBuffer.SetData(_arrows.ToArray());
            
            var material = _meshRenderer.sharedMaterial;
            material.SetBuffer("_ArrowsData", _computeBuffer); 
            material.SetInt("_ArrowsDataSize", _computeBuffer.count);
        }

        [Button]
        private void RefreshRenderer()
        {
            Mesh mesh = new Mesh();

            var      sizeDelta3D = new float3(_sizeDelta, 0.0f);
            float3[] vertices    = new float3[4]
            {
                sizeDelta3D * _corners[0],
                sizeDelta3D * _corners[1],
                sizeDelta3D * _corners[2],
                sizeDelta3D * _corners[3],
            };
            float2[] uvs = new float2[4]
            {
                new float2(0, 0),
                new float2(0, 1),
                new float2(1, 1),
                new float2(1, 0)
            };
            int[]    indices     = new int[4]
            {
                0, 1, 2, 3
            };

            mesh.vertices = vertices.Select(x => (Vector3)x).ToArray();
            mesh.uv       = uvs.Select(x => (Vector2)x).ToArray();
            mesh.SetIndices(indices, MeshTopology.Quads, 0);
            
            mesh.RecalculateNormals();
            
            _meshFilter.mesh = mesh;
        }

        private void OnDestroy()
        {
            _computeBuffer?.Dispose();
        }
    }

    [Serializable]
    public struct Arrow
    {
        public enum ElementType
        {
            Nip,
            Trail
        }

        public enum ElementDirection
        {
            Vertical,
            Horizontal
        }

        public ElementType      elementType;
        public ElementDirection elementDirection;
        public int              elementDirectionNegate;
        public float4           elementPoints;

        public Arrow(ElementType elementType,  ElementDirection elementDirection, bool elementNegate, float4 elementPoints)
        {
            this.elementType            = elementType;
            this.elementDirection       = elementDirection;
            this.elementDirectionNegate = elementNegate ? 1 : 0;
            this.elementPoints          = elementPoints;
        }
    }
}