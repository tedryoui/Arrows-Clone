using System;
using _.Scripts.Utility.Extensions;
using Codice.Client.Common.WebApi.Responses;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace _.Scripts.Editor.UI_Toolkit
{
    [UxmlElement]
    public partial class UIToolkitSceneControl : VisualElement
    {
        private VisualElement               _view;
        private Scene                       _scene;
        private IVisualElementScheduledItem _item;
        
        private RenderTexture _renderTexture;
        private Camera        _camera;

        private float2 _previousMousePosition;
        private bool   _isWheenDown;

        private event Action _onUpdate;

        public Camera Camera => _camera;

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
            
            CreateSceneView();
            
            if (this.panel.contextType is not ContextType.Editor ||
                !this.panel.ToString().Contains("UI Builder"))
            {
                CreateScene();

                _item = this.schedule.Execute(() =>
                {
                    if (!_scene.IsValid() || _camera == null)
                        return;

                    
                    _camera.Render();
                    _view.MarkDirtyRepaint();
                }).Every((uint)(1000 / 120.0f));
            }
        }

        private void OnPanelDetachEvent(DetachFromPanelEvent evt)
        {
            if (_item != null)
                _item.Pause();
            
            if (_view != null)
                Remove(_view);
            
            if (_scene.IsValid())
                EditorSceneManager.CloseScene(_scene, true);
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
            _camera.orthographicSize   = 14;
            _camera.targetTexture      = _renderTexture;
            _camera.transform.position = new Vector3(0, 0, -10);
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
            var deltaTime = 1.0f / 120.0f;
            var zoomSpeed = 10.0f * deltaTime;

            _camera.orthographicSize += zoomSpeed * evt.delta.y;
            
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (_isWheenDown)
            {
                var deltaTime = 1.0f / 120.0f;
                var moveSpeed = 4.4f * deltaTime;
                var delta     = ((float2)evt.mousePosition - _previousMousePosition) * moveSpeed;
                delta.x *= -1;

                var transformLocalPosition = _camera.transform.localPosition;
                transformLocalPosition += new Vector3(delta.x, delta.y);

                _camera.transform.localPosition = transformLocalPosition;
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
    }
}