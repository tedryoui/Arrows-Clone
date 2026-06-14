using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace _.Scripts.Editor.UI_Toolkit
{
    public class SceneRenderer : IDisposable
    {
        private Material gridMaterial;
        private bool _isGridMaterialCreated;
        private Material handleMaterial;
        private Material wireframeMaterial;
        private Camera camera;
        private readonly List<GameObject> _gizmoObjects = new List<GameObject>();

        private GridParameters grid;
        private GridParameters GridPrevious { get; set; }

        public event Action<Camera> OnPreCullGizmos;
        public event Action<Camera> OnPostRenderGizmos;

        public GridParameters Grid
        {
            get => grid;
            set
            {
                grid = value;
                UpdateGridMaterial();
            }
        }
        public Color GizmoColor { get; set; }

        public SceneRenderer()
        {
            Grid = GridParameters.Default;
            GridPrevious = Grid;
            GizmoColor = Color.cyan;
        }

        public void InitializeGrid()
        {
            _isGridMaterialCreated = false;
            gridMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Shaders/Grid.mat");
            if (gridMaterial == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Core/Shaders/Grid Shader.shader");
                if (shader != null)
                {
                    gridMaterial = new Material(shader);
                    _isGridMaterialCreated = true;
                }
            }

            Grid = GridParameters.Default;
            GridPrevious = Grid;
            UpdateGridMaterial();
        }

        public void InitializeGizmoMaterials()
        {
            handleMaterial = new Material(Shader.Find("Sprites/Default"));

            wireframeMaterial = new Material(Shader.Find("Sprites/Default"));
            wireframeMaterial.renderQueue = 3000;
        }

        public void SetCamera(Camera camera) => this.camera = camera;

        public void AddGizmoObject(GameObject gameObject)
        {
            if (gameObject == null || _gizmoObjects.Contains(gameObject))
                return;

            _gizmoObjects.Add(gameObject);
        }

        public void RemoveGizmoObject(GameObject gameObject)
        {
            _gizmoObjects.Remove(gameObject);
        }

        public void ClearGizmoObjects()
        {
            _gizmoObjects.Clear();
        }

        public void RenderGrid(Camera camera, Scene scene)
        {
            this.camera = camera;

            if (gridMaterial == null || !scene.IsValid() || camera == null)
                return;

            if (Grid.color != GridPrevious.color ||
                Grid.lineSize != GridPrevious.lineSize ||
                Grid.fadeOutMin != GridPrevious.fadeOutMin ||
                Grid.fadeOutMax != GridPrevious.fadeOutMax)
            {
                UpdateGridMaterial();
                GridPrevious = Grid;
            }

            float orthoSize = camera.orthographicSize;
            float aspect = camera.aspect;

            gridMaterial.SetVector("_GridBoundsMin", new Vector4(-orthoSize * aspect, -orthoSize, 0, 0));
            gridMaterial.SetVector("_GridBoundsMax", new Vector4(orthoSize * aspect, orthoSize, 0, 0));
            gridMaterial.SetVector("_GridOffset", new Vector4(0.5f, 0.5f, 0, 0));

            var cameraPos = camera.transform.position;

            var matrix = Matrix4x4.TRS(
                new Vector3(cameraPos.x, cameraPos.y, 0),
                Quaternion.identity,
                new Vector3(orthoSize * aspect * 2, orthoSize * 2, 1)
            );

            SceneDrawingUtility.DrawQuad(matrix, camera, gridMaterial);
        }

        public void RenderGizmos(Camera camera, Scene scene)
        {
            this.camera = camera;

            if (wireframeMaterial == null || !scene.IsValid() || camera == null)
                return;

            OnPreCullGizmos?.Invoke(camera);

            foreach (var obj in _gizmoObjects)
            {
                if (obj == null) continue;

                DrawWireframeBounds(obj, wireframeMaterial);
            }

            OnPostRenderGizmos?.Invoke(camera);
        }

        public void DrawHandle(Vector3 position, float size, Color color)
        {
            if (handleMaterial == null || camera == null) return;

            handleMaterial.color = color;
            SceneDrawingUtility.DrawQuad(position, new Vector3(size, size, 1f), camera, handleMaterial);
        }

        public void DrawWireDisc(Vector3 position, float radius, Color color, float thickness = SceneDrawingUtility.DefaultLineThickness)
        {
            if (handleMaterial == null || camera == null) return;

            handleMaterial.color = color;
            SceneDrawingUtility.DrawDisc(position, radius, camera, handleMaterial);
        }

        public void DrawLine(Vector3 start, Vector3 end, Color color, float thickness = SceneDrawingUtility.DefaultLineThickness)
        {
            if (handleMaterial == null || camera == null) return;

            handleMaterial.color = color;
            SceneDrawingUtility.DrawLine(start, end, thickness, camera, handleMaterial);
        }

        public void DrawDot(Vector3 position, float size, Color color)
        {
            DrawHandle(position, size, color);
        }

        public void DrawRay(Vector3 start, Vector3 direction, Color color, float length = SceneDrawingUtility.DefaultRayLength)
        {
            DrawLine(start, start + direction.normalized * length, color);
        }

        public void DrawCross(Vector3 position, float size, Color color)
        {
            if (handleMaterial == null || camera == null) return;

            handleMaterial.color = color;
            float thickness = size * SceneDrawingUtility.CrossThicknessScale;

            SceneDrawingUtility.DrawQuad(position, Quaternion.identity, new Vector3(size, thickness, 1f), camera, handleMaterial);

            SceneDrawingUtility.DrawQuad(position, Quaternion.Euler(0, 0, 90), new Vector3(size, thickness, 1f), camera, handleMaterial);
        }

        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Color color)
        {
            if (mesh == null || handleMaterial == null || camera == null) return;

            handleMaterial.color = color;
            Graphics.DrawMesh(mesh, matrix, handleMaterial, 0, camera);
        }

        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material)
        {
            if (mesh == null || material == null || camera == null) return;

            Graphics.DrawMesh(mesh, matrix, material, 0, camera);
        }

        public void Dispose()
        {
            if (gridMaterial != null && _isGridMaterialCreated)
            {
                Object.DestroyImmediate(gridMaterial);
            }

            gridMaterial = null;
            _isGridMaterialCreated = false;

            if (handleMaterial != null)
            {
                Object.DestroyImmediate(handleMaterial);
                handleMaterial = null;
            }

            if (wireframeMaterial != null)
            {
                Object.DestroyImmediate(wireframeMaterial);
                wireframeMaterial = null;
            }

            camera = null;
            _gizmoObjects.Clear();
        }

        private void DrawWireframeBounds(GameObject obj, Material material)
        {
            if (obj == null) return;

            Bounds bounds = GetObjectBounds(obj);

            DrawWireframeBox(bounds, material);
        }

        private void DrawWireframeBox(Bounds bounds, Material material)
        {
            if (material == null || camera == null) return;

            material.color = GizmoColor;
            SceneDrawingUtility.DrawBox(bounds, camera, material);
        }

        private Bounds GetObjectBounds(GameObject obj)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            return new Bounds(obj.transform.position, Vector3.one * 0.5f);
        }

        private void UpdateGridMaterial()
        {
            if (gridMaterial == null)
                return;

            gridMaterial.SetColor("_GridLineColor", Grid.color);
            gridMaterial.SetFloat("_FadeOutMin", Grid.fadeOutMin);
            gridMaterial.SetFloat("_FadeOutMax", Grid.fadeOutMax);
        }
    }
}
