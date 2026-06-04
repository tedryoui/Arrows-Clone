using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using Unity.Collections;
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
        [SerializeField] private uint         _seed;
        
        

        [SerializeField] private Arrow[]        _arrows;
        [SerializeField] private ArrowElement[] _arrowElements;
        
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

        // private void UpdateArrow()
        // {
        //     if (_arrows == null || _arrows.Le == 0) 
        //         return;
        //     
        //     var arrow     = _arrows[0];
        //     var direction = ArrowElement.FromDirection(arrow.elementDirection, arrow.elementDirectionNegate == 1);
        //
        //     var arrowPosition = arrow.elementPoints.xy;
        //
        //     if (math.distance(arrowPosition, new float2(-5, 0)) >= 0.0f) 
        //     {
        //         arrowPosition += direction * Time.deltaTime * 3;
        //         
        //         arrow.elementPoints = new float4(arrowPosition, 0.0f);
        //         _arrows[0]          = arrow;
        //
        //         var trail = _arrows[1];
        //         trail.elementPoints = new float4(trail.elementPoints.xy, arrowPosition);
        //
        //         _arrows[1] = trail;
        //     }
        //
        //     if (math.distance(arrowPosition, _previousArrowPosition) >= 1.0f)
        //     {
        //         var trail = new ArrowElement(ArrowElement.ElementType.Trail, arrow.elementDirection, arrow.elementDirectionNegate == 1, new float4(arrowPosition, arrowPosition));
        //         
        //         _arrows.Insert(1, trail);
        //
        //         _previousArrowPosition = arrowPosition;
        //     }
        //
        //     if (_arrows.Count > 3)
        //     {
        //         var lastTrail = _arrows[^1];
        //         
        //         var current   = lastTrail.elementPoints.xy;
        //         var currentDirection = ArrowElement.FromDirection(lastTrail.elementDirection, lastTrail.elementDirectionNegate == 1);
        //
        //         current += currentDirection * Time.deltaTime * 3;
        //         
        //         var target  = lastTrail.elementPoints.zw;
        //
        //         lastTrail.elementPoints = new float4(current, target);
        //         _arrows[^1]             = lastTrail;
        //
        //         if (math.distance(target, current) <= 0.05f)
        //         {
        //             _arrows.RemoveAt(_arrows.Count - 1);
        //         }
        //     }
        // }
        
#region Plane Manage

        private void UpdateMaterial()
        {
            if (_computeBuffer != null && _computeBuffer.IsValid())
                _computeBuffer.Dispose();

            if (_arrowElements == null || _arrowElements.Length == 0)
                return;
            
            _computeBuffer = new ComputeBuffer(_arrowElements.Length, 
                sizeof(ArrowElement.ElementType) + 
                sizeof(ArrowElement.ElementDirection) +
                sizeof(int) +
                sizeof(float) * 4);
            _computeBuffer.SetData(_arrowElements.ToArray());
            
            var material = _meshRenderer.sharedMaterial;
            material.SetBuffer("_ArrowsData", _computeBuffer); 
            material.SetInt("_ArrowsDataSize", _computeBuffer.count);
        }

        [Button]
        private void RefreshRenderer()
        {
            
        }

#endregion
        
        private void OnDestroy()
        {
            _computeBuffer?.Dispose();
        }
    }

    [Serializable]
    public struct Arrow : IDisposable
    {
        public uint              index;
        // public NativeArray<uint> elementIndices;

        public void Dispose()
        {
            // elementIndices.Dispose();
        }
    }
    
    [Serializable]
    public struct ArrowElement
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

        public ArrowElement(ElementType elementType,  ElementDirection elementDirection, bool elementNegate, float4 elementPoints)
        {
            this.elementType            = elementType;
            this.elementDirection       = elementDirection;
            this.elementDirectionNegate = elementNegate ? 1 : 0;
            this.elementPoints          = elementPoints;
        }
        
        public static float2 FromDirection(ArrowElement.ElementDirection direction, bool negate)
        {
            var fDir = new float2(0, 0);

            if (direction is ArrowElement.ElementDirection.Horizontal)
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

        public ArrowElement.ElementDirection ToDirection(float2 dir, out bool negate)
        {
            var rDir = math.normalize(dir);
            
            if (rDir is { x: 1, y: 0 })
            {
                negate = false;
                return ArrowElement.ElementDirection.Horizontal;
            }

            if (rDir is { x: -1, y: 0 })
            {
                negate = true;
                return ArrowElement.ElementDirection.Horizontal;
            }

            if (rDir is { x: 0, y: 1 })
            {
                negate = false;
                return ArrowElement.ElementDirection.Vertical;
            }

            if (rDir is { x: 0, y: -1 })
            {
                negate = true;
                return ArrowElement.ElementDirection.Vertical;
            }
            
            throw new Exception("Invalid direction");
        }
    }
}