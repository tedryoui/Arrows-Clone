using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace _.Scripts.Editor.UI_Toolkit
{
    [Serializable]
    public struct GridParameters
    {
        public Color color;
        public float lineSize;
        public float fadeOutMin;
        public float fadeOutMax;
        
        public static GridParameters Default => new GridParameters
        {
            color = new Color(0.7f, 0.7f, 0.7f, 1.0f),
            lineSize = 0.02f,
            fadeOutMin = 0.1f,
            fadeOutMax = 1.0f
        };
    }
    
    [UxmlElement]
    public partial class UIToolkitSceneControl : VisualElement
    {
        private VisualElement               _view;
        private Scene                       _scene;
        private IVisualElementScheduledItem _item;
        
        private RenderTexture _renderTexture;
        private Camera        _camera;

        private float2  _previousMousePosition;
        private bool    _isWheenDown;
        private Vector3 _targetCameraPosition;
        private float   _targetCameraSize;
        private float   _cameraSpeed;

        private event Action _onUpdate;
        
        private Material _gridMaterial;
        private Mesh _quadMesh;
        private GridParameters _grid;
        private GridParameters _gridPrevious;

        // Handles and Gizmos support
        private List<GameObject> _gizmoObjects = new List<GameObject>();
        private Material _handleMaterial;
        private Material _wireframeMaterial;
        private Color _gizmoColor = Color.cyan;
        
        // Mouse position in world space for cell highlighting
        private Vector2 _mouseWorldPosition;
        private bool _hasMousePosition;
        
        // Event for custom gizmo drawing
        public event Action<Camera>          OnPreCullGizmos;
        public event Action<Camera>          OnPostRenderGizmos;
        public event Action<MouseEnterEvent> OnMouseEnterView;
        public event Action<MouseOutEvent>   OnMouseOutView;
        public event Action<MouseMoveEvent>  OnMouseMoveView;
        public event Action<MouseDownEvent>  OnMouseDownView;
        public event Action<MouseUpEvent>    OnMouseUpView;

        public Camera Camera => _camera;
        public Scene Scene => _scene;

        public float DeltaTime => 1.0f / 100f;
        
        public GridParameters Grid
        {
            get => _grid;
            set
            {
                _grid = value;
                UpdateGridMaterial();
            }
        }
        
        /// <summary>
        /// Color used for drawing gizmos
        /// </summary>
        public Color GizmoColor
        {
            get => _gizmoColor;
            set => _gizmoColor = value;
        }

        public UIToolkitSceneControl()
        {
            RegisterCallback<AttachToPanelEvent>(OnPanelAttachEvent);
            RegisterCallback<DetachFromPanelEvent>(OnPanelDetachEvent);
        }

        private void OnPanelAttachEvent(AttachToPanelEvent evt)
        {
            int width  = 480;
            int height = 680;
            
            _renderTexture =
                new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _renderTexture.Create();

            _targetCameraPosition = Vector3.forward * -10;
            _targetCameraSize     = 7f;
            _cameraSpeed          = 4.0f;
            
            CreateSceneView();
            
            if (this.panel.contextType is not ContextType.Editor ||
                !this.panel.ToString().Contains("UI Builder"))
            {
                CreateScene();
                InitializeGrid();
                InitializeGizmoMaterials();

                _item = this.schedule.Execute(() =>
                 {
                     if (!_scene.IsValid() || _camera == null)
                         return;

                     _onUpdate?.Invoke();
                     UpdateCamera();
                     RenderGrid();
                     RenderGizmos();
                     _camera.Render();
                     _view.MarkDirtyRepaint();
                 }).Every((uint)(DeltaTime * 1000.0f));
            }
        }

        private void UpdateCamera()
        {
            var currentPosition = math.lerp(_camera.transform.localPosition, _targetCameraPosition, DeltaTime * _cameraSpeed);
            _camera.transform.localPosition = currentPosition;
            
            var currentOrthographicSize = math.lerp(_camera.orthographicSize, _targetCameraSize, DeltaTime * _cameraSpeed);
            _camera.orthographicSize = currentOrthographicSize;
        }

        private void OnPanelDetachEvent(DetachFromPanelEvent evt)
        {
            if (_item != null)
                _item.Pause();
            
            if (_view != null)
                Remove(_view);
            
            if (_gridMaterial != null)
            {
                Object.DestroyImmediate(_gridMaterial);
                _gridMaterial = null;
            }
            
            if (_handleMaterial != null)
            {
                Object.DestroyImmediate(_handleMaterial);
                _handleMaterial = null;
            }
            
            if (_wireframeMaterial != null)
            {
                Object.DestroyImmediate(_wireframeMaterial);
                _wireframeMaterial = null;
            }
            
            if (_scene.IsValid())
                EditorSceneManager.CloseScene(_scene, true);
        }
        
        private void InitializeGrid()
        {
            _quadMesh = GetQuadMesh();
            _gridMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_/Shaders/Grid.mat");
            if (_gridMaterial == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_/Shaders/Grid Shader.shader");
                if (shader != null)
                {
                    _gridMaterial = new Material(shader);
                }
            }
            _grid = GridParameters.Default;
            _gridPrevious = _grid;
            UpdateGridMaterial();
        }
        
        private void InitializeGizmoMaterials()
        {
            // Create material for handles
            _handleMaterial = new Material(Shader.Find("Sprites/Default"));
            
            // Create wireframe material for drawing bounds
            _wireframeMaterial = new Material(Shader.Find("Sprites/Default"));
            _wireframeMaterial.renderQueue = 3000; // Transparent queue
        }

        private void RenderGrid()
        {
            if (_gridMaterial == null || _quadMesh == null || !_scene.IsValid())
                return;
            
            if (_grid.color != _gridPrevious.color || 
                _grid.lineSize != _gridPrevious.lineSize ||
                _grid.fadeOutMin != _gridPrevious.fadeOutMin ||
                _grid.fadeOutMax != _gridPrevious.fadeOutMax)
            {
                UpdateGridMaterial();
                _gridPrevious = _grid;
            }
            
            float orthoSize = _camera.orthographicSize;
            float aspect = _camera.aspect;
            
            _gridMaterial.SetVector("_GridBoundsMin", new Vector4(-orthoSize * aspect, -orthoSize, 0, 0));
            _gridMaterial.SetVector("_GridBoundsMax", new Vector4(orthoSize * aspect, orthoSize, 0, 0));
            _gridMaterial.SetVector("_GridOffset", new  Vector4(0.5f, 0.5f, 0, 0));
            
            var cameraPos = _camera.transform.position;
            
            var matrix = Matrix4x4.TRS(
                new Vector3(cameraPos.x, cameraPos.y, 0),
                Quaternion.identity,
                new Vector3(orthoSize * aspect * 2, orthoSize * 2, 1)
            );
            
            Graphics.DrawMesh(_quadMesh, matrix, _gridMaterial, 0, _camera);
        }
        
        private void RenderGizmos()
        {
            if (_wireframeMaterial == null || !_scene.IsValid())
                return;
            
            // Invoke pre-cull event
            OnPreCullGizmos?.Invoke(_camera);
            
            // Draw gizmos for all registered objects
            foreach (var obj in _gizmoObjects)
            {
                if (obj == null) continue;
                
                // Draw wireframe bounds
                DrawWireframeBounds(obj, _wireframeMaterial);
            }
            
            // Invoke post-render event
            OnPostRenderGizmos?.Invoke(_camera);
        }
        
        private void DrawWireframeBounds(GameObject obj, Material material)
        {
            if (obj == null) return;
            
            // Get the bounds of the object
            Bounds bounds = GetObjectBounds(obj);
            
            // Draw wireframe using multiple quads to represent the edges
            DrawWireframeBox(bounds, material);
        }
        
        private void DrawWireframeBox(Bounds bounds, Material material)
        {
            // For 2D/orthographic view, we draw a simple wireframe rectangle
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            
            // Set the gizmo color
            _wireframeMaterial.color = _gizmoColor;
            
            // Draw the four edges of the rectangle
            // Top edge
            DrawLineQuad(new Vector3(center.x - size.x/2, center.y + size.y/2, center.z), 
                        new Vector3(center.x + size.x/2, center.y + size.y/2, center.z), 
                        0.02f, material);
            
            // Bottom edge
            DrawLineQuad(new Vector3(center.x - size.x/2, center.y - size.y/2, center.z), 
                        new Vector3(center.x + size.x/2, center.y - size.y/2, center.z), 
                        0.02f, material);
            
            // Left edge
            DrawLineQuad(new Vector3(center.x - size.x/2, center.y - size.y/2, center.z), 
                        new Vector3(center.x - size.x/2, center.y + size.y/2, center.z), 
                        0.02f, material);
            
            // Right edge
            DrawLineQuad(new Vector3(center.x + size.x/2, center.y - size.y/2, center.z), 
                        new Vector3(center.x + size.x/2, center.y + size.y/2, center.z), 
                        0.02f, material);
        }
        
        private void DrawLineQuad(Vector3 start, Vector3 end, float thickness, Material material)
        {
            var direction = end - start;
            var distance = direction.magnitude;
            var rotation = Quaternion.FromToRotation(Vector3.right, direction);
            
            var matrix = Matrix4x4.TRS(
                start + direction * 0.5f,
                rotation,
                new Vector3(distance, thickness, 1)
            );
            
            Graphics.DrawMesh(_quadMesh, matrix, material, 0, _camera);
        }
        
        private Bounds GetObjectBounds(GameObject obj)
        {
            // Try to get renderer bounds
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }
            
            // Try collider bounds
            var collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }
            
            // Default to a small box at the object's position
            return new Bounds(obj.transform.position, Vector3.one * 0.5f);
        }

        private void UpdateGridMaterial()
        {
            if (_gridMaterial == null)
                return;
            
            _gridMaterial.SetColor("_GridLineColor", _grid.color);
            _gridMaterial.SetFloat("_FadeOutMin", _grid.fadeOutMin);
            _gridMaterial.SetFloat("_FadeOutMax", _grid.fadeOutMax);
        }
        
        /// <summary>
        /// Add a GameObject to the gizmo rendering list
        /// </summary>
        public void AddGizmoObject(GameObject gameObject)
        {
            if (!_gizmoObjects.Contains(gameObject))
            {
                _gizmoObjects.Add(gameObject);
            }
        }
        
        /// <summary>
        /// Remove a GameObject from the gizmo rendering list
        /// </summary>
        public void RemoveGizmoObject(GameObject gameObject)
        {
            _gizmoObjects.Remove(gameObject);
        }
        
        /// <summary>
        /// Clear all gizmo objects
        /// </summary>
        public void ClearGizmoObjects()
        {
            _gizmoObjects.Clear();
        }
        
        /// <summary>
        /// Draw a handle at the specified position in the preview scene
        /// </summary>
        public void DrawHandle(Vector3 position, float size, Color color)
        {
            if (_handleMaterial == null || _quadMesh == null) return;
            
            _handleMaterial.color = color;
            
            var matrix = Matrix4x4.TRS(
                position,
                Quaternion.identity,
                new Vector3(size, size, 1)
            );
            
            Graphics.DrawMesh(_quadMesh, matrix, _handleMaterial, 0, _camera);
        }
        
        /// <summary>
        /// Draw a wire disc (2D circle) at the specified position
        /// </summary>
        public void DrawWireDisc(Vector3 position, float radius, Color color, float thickness = 0.02f)
        {
            if (_handleMaterial == null || _quadMesh == null) return;
            
            _handleMaterial.color = color;
            
            // Draw a disc using a quad scaled to the radius
            var matrix = Matrix4x4.TRS(
                position,
                Quaternion.identity,
                new Vector3(radius * 2, radius * 2, 1)
            );
            
            Graphics.DrawMesh(_quadMesh, matrix, _handleMaterial, 0, _camera);
        }
        
        /// <summary>
        /// Draw a line in the preview scene
        /// </summary>
        public void DrawLine(Vector3 start, Vector3 end, Color color, float thickness = 0.02f)
        {
            if (_handleMaterial == null || _quadMesh == null) return;
            
            _handleMaterial.color = color;
            
            var direction = end - start;
            var distance = direction.magnitude;
            var rotation = Quaternion.FromToRotation(Vector3.right, direction);
            
            var matrix = Matrix4x4.TRS(
                start + direction * 0.5f,
                rotation,
                new Vector3(distance, thickness, 1)
            );
            
            Graphics.DrawMesh(_quadMesh, matrix, _handleMaterial, 0, _camera);
        }
        
        /// <summary>
        /// Draw a dot at the specified position
        /// </summary>
        public void DrawDot(Vector3 position, float size, Color color)
        {
            DrawHandle(position, size, color);
        }
        
        /// <summary>
        /// Draw a ray from start position in a direction
        /// </summary>
        public void DrawRay(Vector3 start, Vector3 direction, Color color, float length = 100f)
        {
            DrawLine(start, start + direction.normalized * length, color);
        }
        
        /// <summary>
         /// Draw a cross (plus sign) at the specified position
         /// </summary>
         public void DrawCross(Vector3 position, float size, Color color)
         {
             if (_handleMaterial == null || _quadMesh == null) return;
             
             _handleMaterial.color = color;
             
             // Draw horizontal line
             var matrixH = Matrix4x4.TRS(
                 position,
                 Quaternion.identity,
                 new Vector3(size, size * 0.2f, 1)
             );
             Graphics.DrawMesh(_quadMesh, matrixH, _handleMaterial, 0, _camera);
             
             // Draw vertical line
             var matrixV = Matrix4x4.TRS(
                 position,
                 Quaternion.Euler(0, 0, 90),
                 new Vector3(size, size * 0.2f, 1)
             );
             Graphics.DrawMesh(_quadMesh, matrixV, _handleMaterial, 0, _camera);
         }
         
         /// <summary>
         /// Draw a custom mesh in the preview scene
         /// </summary>
         /// <param name="mesh">The mesh to draw</param>
         /// <param name="matrix">The transformation matrix for the mesh</param>
         /// <param name="color">The color to use for rendering</param>
         public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Color color)
         {
             if (mesh == null || _handleMaterial == null || _camera == null) return;
             
             _handleMaterial.color = color;
             Graphics.DrawMesh(mesh, matrix, _handleMaterial, 0, _camera);
         }
         
         public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material)
         {
             if (mesh == null || material == null || _camera == null) return;
             
             Graphics.DrawMesh(mesh, matrix, material, 0, _camera);
         }

         private static Mesh s_QuadMesh;
        
        private Mesh GetQuadMesh()
        {
            if (s_QuadMesh != null)
                return s_QuadMesh;
                
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0),
                new Vector3(0.5f, 0.5f, 0)
            };
            mesh.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateNormals();
            s_QuadMesh = mesh;
            return mesh;
        }

        private void CreateScene()
        {
            _scene = EditorSceneManager.NewPreviewScene();

            _camera                    = new GameObject("Main Camera").AddComponent<Camera>();
            _camera.cameraType         = CameraType.SceneView;
            _camera.clearFlags         = CameraClearFlags.Nothing;
            _camera.orthographic       = true;
            _camera.transform.forward  = Vector3.forward;
            _camera.enabled            = false;
            _camera.orthographicSize   = _targetCameraSize;
            _camera.targetTexture      = _renderTexture;
            _camera.transform.position = _targetCameraPosition;
            _camera.scene              = _scene;
            
            EditorSceneManager.MoveGameObjectToScene(_camera.gameObject, _scene);
        }

        private void CreateSceneView()
        {
            _view = new VisualElement();

            _view.name = "View";
            _view.AddToClassList("scene-control__view");
            
            _view.style.flexGrow        = 1;
            _view.style.backgroundImage = Background.FromRenderTexture(_renderTexture);
            _view.style.backgroundColor = Color.clear;
            
            _view.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _view.RegisterCallback<MouseUpEvent>(OnMouseUp);
            _view.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            _view.RegisterCallback<MouseOutEvent>(OnMouseOut);
            _view.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _view.RegisterCallback<WheelEvent>(OnWheel);
            
            Add(_view);
        }

        private void OnWheel(WheelEvent evt)
        {
            var deltaTime = DeltaTime;
            var zoomSpeed = 18.0f * deltaTime;
            
            _targetCameraSize += zoomSpeed * evt.delta.y;
            _targetCameraSize = math.clamp(_targetCameraSize, 7f, 14f);
            
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
         {
             if (_isWheenDown)
             {
                 var deltaTime = DeltaTime;
                 var moveSpeed = 4.0f * deltaTime;
                 var delta     = ((float2)evt.mousePosition - _previousMousePosition) * moveSpeed;
                 delta.x *= -1;

                 var transformLocalPosition = _targetCameraPosition;
                 transformLocalPosition += new Vector3(delta.x, delta.y);

                 _targetCameraPosition = transformLocalPosition;
                 _previousMousePosition = evt.mousePosition;
             }
             
             OnMouseMoveView?.Invoke(evt);
             
             evt.StopPropagation();
         }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button is 2 && _isWheenDown)
                _isWheenDown = false;
            
            OnMouseUpView?.Invoke(evt);
            
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button is 2)
            {
                _isWheenDown = true;
                _previousMousePosition = evt.mousePosition;
            }
            
            OnMouseDownView?.Invoke(evt);
            
            evt.StopPropagation();
        }

        public void AttachGameObjectToScene(GameObject gameObject)
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.MoveGameObjectToScene(gameObject, _scene);
            }
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            OnMouseEnterView?.Invoke(evt);
            
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt)
         {
             _isWheenDown = false;
             
             OnMouseOutView?.Invoke(evt);
             
             evt.StopPropagation();
         }

        /// <summary>
        /// Convert screen position to world position in the preview scene
        /// </summary>
        private Vector2 ConvertScreenToWorldPosition(Vector2 screenPosition)
        {
            if (_camera == null) return Vector2.zero;
            
            // Get the view dimensions
            var viewWidth = _view.resolvedStyle.width;
            var viewHeight = _view.resolvedStyle.height;
            
            // Convert screen position to normalized viewport coordinates (0-1)
            var normalizedX = screenPosition.x / viewWidth;
            var normalizedY = screenPosition.y / viewHeight;
            
            // Convert to world coordinates
            // The camera shows a view of orthographicSize * 2 in height
            // and orthographicSize * 2 * aspect in width
            var orthoSize = _camera.orthographicSize;
            var aspect = _camera.aspect;
            
            // World position relative to camera center
            var worldX = (normalizedX - 0.5f) * orthoSize * aspect * 2;
            var worldY = (0.5f - normalizedY) * orthoSize * 2;
            
            // Add camera position to get world position
            var cameraPos = _camera.transform.localPosition;
            return new Vector2(cameraPos.x + worldX, cameraPos.y + worldY);
        }

        public bool IsValid()
        {
            return _scene.IsValid();
        }

        public void OnUpdate(Action callback)
        {
            _onUpdate = callback;
        }

        public void SetCameraPosition(Vector3 localPosition)
        {
            _targetCameraPosition = localPosition;
        }

        public void SetCameraSize(float orthographicSize)
        {
            _targetCameraSize = orthographicSize;
        }
    }
}