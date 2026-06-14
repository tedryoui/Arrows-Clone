using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        #region Private Fields

        private VisualElement _view;
        private RenderTexture _renderTexture;
        private IVisualElementScheduledItem _scheduledUpdate;
        private Scene _scene;
        private Camera _camera;
        private SceneInputHandler _inputHandler;
        private SceneRenderer _renderer;
        private event Action _updateCallback;

        #endregion

        #region Events

        public event Action<Camera> OnPreCullGizmos;
        public event Action<Camera> OnPostRenderGizmos;
        public event Action<MouseEnterEvent> OnMouseEnterView;
        public event Action<MouseOutEvent> OnMouseOutView;
        public event Action<MouseMoveEvent> OnMouseMoveView;
        public event Action<MouseDownEvent> OnMouseDownView;
        public event Action<MouseUpEvent> OnMouseUpView;

        #endregion

        #region Properties

        public Camera Camera => _camera;
        public Scene Scene => _scene;
        public float DeltaTime => 1.0f / 100f;

        public GridParameters Grid
        {
            get => GetRenderer().Grid;
            set => GetRenderer().Grid = value;
        }

        public Color GizmoColor
        {
            get => GetRenderer().GizmoColor;
            set => GetRenderer().GizmoColor = value;
        }

        #endregion

        #region Public API

        public UIToolkitSceneControl()
        {
            RegisterCallback<AttachToPanelEvent>(OnPanelAttachEvent);
            RegisterCallback<DetachFromPanelEvent>(OnPanelDetachEvent);
        }

        public void AddGizmoObject(GameObject gameObject)
        {
            GetRenderer().AddGizmoObject(gameObject);
        }

        public void RemoveGizmoObject(GameObject gameObject)
        {
            GetRenderer().RemoveGizmoObject(gameObject);
        }

        public void ClearGizmoObjects()
        {
            GetRenderer().ClearGizmoObjects();
        }

        public void DrawHandle(Vector3 position, float size, Color color)
        {
            _renderer?.DrawHandle(position, size, color);
        }

        public void DrawWireDisc(Vector3 position, float radius, Color color, float thickness = SceneDrawingUtility.DefaultLineThickness)
        {
            _renderer?.DrawWireDisc(position, radius, color, thickness);
        }

        public void DrawLine(Vector3 start, Vector3 end, Color color, float thickness = SceneDrawingUtility.DefaultLineThickness)
        {
            _renderer?.DrawLine(start, end, color, thickness);
        }

        public void DrawDot(Vector3 position, float size, Color color)
        {
            _renderer?.DrawDot(position, size, color);
        }

        public void DrawRay(Vector3 start, Vector3 direction, Color color, float length = SceneDrawingUtility.DefaultRayLength)
        {
            _renderer?.DrawRay(start, direction, color, length);
        }

        public void DrawCross(Vector3 position, float size, Color color)
        {
            _renderer?.DrawCross(position, size, color);
        }

        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Color color)
        {
            _renderer?.DrawMesh(mesh, matrix, color);
        }

        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material)
        {
            _renderer?.DrawMesh(mesh, matrix, material);
        }

        public void AttachGameObjectToScene(GameObject gameObject)
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.MoveGameObjectToScene(gameObject, _scene);
            }
        }

        public bool IsValid()
        {
            return _scene.IsValid();
        }

        public void OnUpdate(Action callback)
        {
            _updateCallback = callback;
        }

        public void SetCameraPosition(Vector3 localPosition)
        {
            if (_inputHandler != null)
                _inputHandler.TargetCameraPosition = localPosition;
        }

        public void SetCameraSize(float orthographicSize)
        {
            if (_inputHandler != null)
                _inputHandler.TargetCameraSize = orthographicSize;
        }

        #endregion

        #region Unity/UI Toolkit Lifecycle

        private void OnPanelAttachEvent(AttachToPanelEvent evt)
        {
            int width = 480;
            int height = 680;
            
            _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _renderTexture.Create();

            CreateSceneView();

            _inputHandler = new SceneInputHandler(_view, () => DeltaTime);
            _renderer = GetRenderer();

            ResetInput();
            WireInputHandlerEvents();
            WireRendererEvents();

            if (!IsUiBuilder())
            {
                CreateScene();
                InitializeRenderer();
                ScheduleUpdate();
            }
        }

        private void OnPanelDetachEvent(DetachFromPanelEvent evt)
        {
            if (_scheduledUpdate != null)
                _scheduledUpdate.Pause();

            UnwireInputHandlerEvents();
            UnwireRendererEvents();

            _inputHandler?.Dispose();
            _renderer?.Dispose();
            _inputHandler = null;
            _renderer = null;

            if (_view != null)
                Remove(_view);

            if (_renderTexture != null)
            {
                if (_camera != null)
                    _camera.targetTexture = null;

                Object.DestroyImmediate(_renderTexture);
                _renderTexture = null;
            }

            if (_scene.IsValid())
                EditorSceneManager.CloseScene(_scene, true);
        }

        private void CreateScene()
        {
            _scene = EditorSceneManager.NewPreviewScene();

            _camera = new GameObject("Main Camera").AddComponent<Camera>();
            _camera.cameraType = CameraType.SceneView;
            _camera.clearFlags = CameraClearFlags.Nothing;
            _camera.orthographic = true;
            _camera.transform.forward = Vector3.forward;
            _camera.enabled = false;
            _camera.orthographicSize = _inputHandler.TargetCameraSize;
            _camera.targetTexture = _renderTexture;
            _camera.transform.position = _inputHandler.TargetCameraPosition;
            _camera.scene = _scene;

            EditorSceneManager.MoveGameObjectToScene(_camera.gameObject, _scene);
        }

        private void CreateSceneView()
        {
            _view = new VisualElement();

            _view.name = "View";
            _view.AddToClassList("scene-control__view");

            _view.style.flexGrow = 1;
            _view.style.backgroundImage = Background.FromRenderTexture(_renderTexture);
            _view.style.backgroundColor = Color.clear;

            Add(_view);
        }

        #endregion

        #region Private Methods

        private SceneRenderer GetRenderer()
        {
            _renderer ??= new SceneRenderer();
            return _renderer;
        }

        private void ResetInput()
        {
            _inputHandler.Reset(Vector3.forward * -10, 7f, 4.0f);
        }

        private bool IsUiBuilder()
        {
            return this.panel.contextType is ContextType.Editor &&
                   this.panel.ToString().Contains("UI Builder");
        }

        private void InitializeRenderer()
        {
            _renderer.InitializeGrid();
            _renderer.InitializeGizmoMaterials();
        }

        private void ScheduleUpdate()
        {
            _scheduledUpdate = this.schedule.Execute(UpdateScene).Every((uint)(DeltaTime * 1000.0f));
        }

        private void UpdateScene()
        {
            if (!_scene.IsValid() || _camera == null || _renderer == null || _inputHandler == null || _view == null)
                return;

            _renderer.SetCamera(_camera);
            _updateCallback?.Invoke();
            _inputHandler.UpdateCamera(_camera);
            _renderer.RenderGrid(_camera, _scene);
            _renderer.RenderGizmos(_camera, _scene);
            _camera.Render();
            _view.MarkDirtyRepaint();
        }

        private void WireInputHandlerEvents()
        {
            _inputHandler.MouseEnter += ForwardMouseEnter;
            _inputHandler.MouseOut += ForwardMouseOut;
            _inputHandler.MouseMove += ForwardMouseMove;
            _inputHandler.MouseDown += ForwardMouseDown;
            _inputHandler.MouseUp += ForwardMouseUp;
        }

        private void UnwireInputHandlerEvents()
        {
            if (_inputHandler == null)
                return;

            _inputHandler.MouseEnter -= ForwardMouseEnter;
            _inputHandler.MouseOut -= ForwardMouseOut;
            _inputHandler.MouseMove -= ForwardMouseMove;
            _inputHandler.MouseDown -= ForwardMouseDown;
            _inputHandler.MouseUp -= ForwardMouseUp;
        }

        private void WireRendererEvents()
        {
            _renderer.OnPreCullGizmos += ForwardPreCullGizmos;
            _renderer.OnPostRenderGizmos += ForwardPostRenderGizmos;
        }

        private void UnwireRendererEvents()
        {
            if (_renderer == null)
                return;

            _renderer.OnPreCullGizmos -= ForwardPreCullGizmos;
            _renderer.OnPostRenderGizmos -= ForwardPostRenderGizmos;
        }

        private void ForwardPreCullGizmos(Camera camera)
        {
            OnPreCullGizmos?.Invoke(camera);
        }

        private void ForwardPostRenderGizmos(Camera camera)
        {
            OnPostRenderGizmos?.Invoke(camera);
        }

        private void ForwardMouseEnter(MouseEnterEvent evt)
        {
            OnMouseEnterView?.Invoke(evt);
        }

        private void ForwardMouseOut(MouseOutEvent evt)
        {
            OnMouseOutView?.Invoke(evt);
        }

        private void ForwardMouseMove(MouseMoveEvent evt)
        {
            OnMouseMoveView?.Invoke(evt);
        }

        private void ForwardMouseDown(MouseDownEvent evt)
        {
            OnMouseDownView?.Invoke(evt);
        }

        private void ForwardMouseUp(MouseUpEvent evt)
        {
            OnMouseUpView?.Invoke(evt);
        }

        #endregion
    }
}
