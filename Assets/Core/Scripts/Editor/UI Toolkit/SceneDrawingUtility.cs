using UnityEngine;

namespace _.Scripts.Editor.UI_Toolkit
{
    public static class SceneDrawingUtility
    {
        public const float DefaultLineThickness = 0.02f;
        public const float DefaultRayLength = 100f;
        public const float CrossThicknessScale = 0.2f;

        private static Mesh s_QuadMesh;

        public static Mesh QuadMesh
        {
            get
            {
                if (s_QuadMesh == null)
                    s_QuadMesh = CreateQuadMesh();

                return s_QuadMesh;
            }
        }

        public static void DrawQuad(Vector3 position, Vector3 scale, Camera camera, Material material, int layer = 0)
        {
            DrawQuad(Matrix4x4.TRS(position, Quaternion.identity, scale), camera, material, layer);
        }

        public static void DrawQuad(Vector3 position, Quaternion rotation, Vector3 scale, Camera camera, Material material, int layer = 0)
        {
            DrawQuad(Matrix4x4.TRS(position, rotation, scale), camera, material, layer);
        }

        public static void DrawQuad(Matrix4x4 matrix, Camera camera, Material material, int layer = 0)
        {
            if (material == null)
                return;

            Graphics.DrawMesh(QuadMesh, matrix, material, layer, camera);
        }

        public static void DrawLine(Vector3 start, Vector3 end, float thickness, Camera camera, Material material, int layer = 0)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.right, direction);
            Matrix4x4 matrix = Matrix4x4.TRS(start + direction * 0.5f, rotation, new Vector3(distance, thickness, 1f));

            DrawQuad(matrix, camera, material, layer);
        }

        public static void DrawBox(Bounds bounds, Camera camera, Material material, float thickness = DefaultLineThickness, int layer = 0)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;

            DrawLine(new Vector3(center.x - size.x * 0.5f, center.y + size.y * 0.5f, center.z),
                new Vector3(center.x + size.x * 0.5f, center.y + size.y * 0.5f, center.z),
                thickness, camera, material, layer);

            DrawLine(new Vector3(center.x - size.x * 0.5f, center.y - size.y * 0.5f, center.z),
                new Vector3(center.x + size.x * 0.5f, center.y - size.y * 0.5f, center.z),
                thickness, camera, material, layer);

            DrawLine(new Vector3(center.x - size.x * 0.5f, center.y - size.y * 0.5f, center.z),
                new Vector3(center.x - size.x * 0.5f, center.y + size.y * 0.5f, center.z),
                thickness, camera, material, layer);

            DrawLine(new Vector3(center.x + size.x * 0.5f, center.y - size.y * 0.5f, center.z),
                new Vector3(center.x + size.x * 0.5f, center.y + size.y * 0.5f, center.z),
                thickness, camera, material, layer);
        }

        public static void DrawDisc(Vector3 position, float radius, Camera camera, Material material, int layer = 0)
        {
            DrawQuad(position, Quaternion.identity, new Vector3(radius * 2f, radius * 2f, 1f), camera, material, layer);
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();

            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };

            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
