using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace _.Scripts.Utility.Extensions
{
    public static class GameObjectExtension
    {
        private static float3[] Corners = new  float3[4]
        {
            new float3(-0.5f, -0.5f, 0.0f),
            new float3(-0.5f, 0.5f,  0.0f),
            new float3(0.5f,  0.5f,  0.0f),
            new float3(0.5f,  -0.5f, 0.0f)
        };
        
        public static UnityEngine.GameObject CreatePrimitive_Plane(string name, float2 sizeDelta)
        {
            var gameObject   = new UnityEngine.GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            var meshFilter   = gameObject.GetComponent<MeshFilter>();
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            
            Mesh mesh = new Mesh();

            var      sizeDelta3D = new float3(sizeDelta, 0.0f);
            float3[] vertices    = new float3[4]
            {
                sizeDelta3D * Corners[0] ,
                sizeDelta3D * Corners[1],
                sizeDelta3D * Corners[2],
                sizeDelta3D * Corners[3],
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
            
            meshFilter.mesh = mesh;

            return gameObject;
        }
    }
}