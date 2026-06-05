using System;
using _.Scripts.Utility.Extensions;
using Codice.Client.Common.WebApi.Responses;
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

        public UIToolkitSceneControl()
        {
            RegisterCallback<AttachToPanelEvent>(OnPanelAttachEvent);
            RegisterCallback<DetachFromPanelEvent>(OnPanelDetachEvent);
        }

        private void OnPanelAttachEvent(AttachToPanelEvent evt)
        {
            _renderTexture =
                new RenderTexture(480, 720, 24, RenderTextureFormat.ARGB32);
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

                _item = this.schedule.Execute(() =>
                 {
                     if (!_scene.IsValid() || _camera == null)
                         return;

                     _onUpdate?.Invoke();
                     UpdateCamera();
                     RenderGrid();
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
            
            _gridMaterial.SetFloat("_OrthoSize", orthoSize);
            
            var cameraPos = _camera.transform.position;
            
            var matrix = Matrix4x4.TRS(
                new Vector3(cameraPos.x, cameraPos.y, 1),
                Quaternion.identity,
                new Vector3(orthoSize * aspect * 2, orthoSize * 2, 1)
            );
            
            Graphics.DrawMesh(_quadMesh, matrix, _gridMaterial, 0, _camera);
        }
        
        private void UpdateGridMaterial()
        {
            if (_gridMaterial == null)
                return;
            
            _gridMaterial.SetColor("_GridLineColor", _grid.color);
            _gridMaterial.SetFloat("_FadeOutMin", _grid.fadeOutMin);
            _gridMaterial.SetFloat("_FadeOutMax", _grid.fadeOutMax);
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
            _view.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _view.RegisterCallback<WheelEvent>(OnWheel);
            _view.RegisterCallback<MouseOutEvent>(OnMouseOut);
            
            Add(_view);
        }

        private void OnWheel(WheelEvent evt)
        {
            var deltaTime = DeltaTime;
            var zoomSpeed = 18.0f * deltaTime;

            if (_targetCameraSize > 7f || evt.delta.y > 0.0f)
                _targetCameraSize += zoomSpeed * evt.delta.y;
            else
                _targetCameraSize = 7f;
            
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
            
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button is 2 && _isWheenDown)
                _isWheenDown = false;
            
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button is 2)
            {
                _isWheenDown = true;
                _previousMousePosition = evt.mousePosition;
            }
            
            evt.StopPropagation();
        }

        public void AttachGameObjectToScene(GameObject gameObject)
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.MoveGameObjectToScene(gameObject, _scene);
            }
        }

        private void OnMouseOut(MouseOutEvent evt)
        {
            _isWheenDown = false;
            
            evt.StopPropagation();
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