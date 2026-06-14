using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace _.Scripts.Editor.UI_Toolkit
{
    public class SceneInputHandler : IDisposable
    {
        private readonly VisualElement _view;
        private readonly Func<float> _deltaTimeProvider;

        private bool _isWheelDown;
        private Vector2 _previousMousePosition;
        private Vector3 _targetCameraPosition;
        private float _targetCameraSize;
        private float _cameraSpeed;

        public Action<MouseEnterEvent> MouseEnter;
        public Action<MouseOutEvent> MouseOut;
        public Action<MouseMoveEvent> MouseMove;
        public Action<MouseDownEvent> MouseDown;
        public Action<MouseUpEvent> MouseUp;

        public Vector3 TargetCameraPosition
        {
            get => _targetCameraPosition;
            set => _targetCameraPosition = value;
        }

        public float TargetCameraSize
        {
            get => _targetCameraSize;
            set => _targetCameraSize = value;
        }

        public float CameraSpeed
        {
            get => _cameraSpeed;
            set => _cameraSpeed = value;
        }

        public SceneInputHandler(VisualElement view, Func<float> deltaTimeProvider)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _deltaTimeProvider = deltaTimeProvider ?? throw new ArgumentNullException(nameof(deltaTimeProvider));

            _view.RegisterCallback<MouseDownEvent>(OnMouseDown);
            _view.RegisterCallback<MouseUpEvent>(OnMouseUp);
            _view.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            _view.RegisterCallback<MouseOutEvent>(OnMouseOut);
            _view.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _view.RegisterCallback<WheelEvent>(OnWheel);
        }

        public void Reset(Vector3 targetCameraPosition, float targetCameraSize, float cameraSpeed)
        {
            _targetCameraPosition = targetCameraPosition;
            _targetCameraSize = targetCameraSize;
            _cameraSpeed = cameraSpeed;
            _isWheelDown = false;
            _previousMousePosition = Vector2.zero;
        }

        public void UpdateCamera(Camera camera)
        {
            if (camera == null)
                return;

            var deltaTime = _deltaTimeProvider();
            var smoothing = deltaTime * _cameraSpeed;

            camera.transform.localPosition = math.lerp(camera.transform.localPosition, _targetCameraPosition, smoothing);
            camera.orthographicSize = math.lerp(camera.orthographicSize, _targetCameraSize, smoothing);
        }

        public Vector2 ConvertScreenToWorldPosition(Vector2 screenPosition, Camera camera, VisualElement view)
        {
            if (camera == null || view == null)
                return Vector2.zero;

            var viewWidth = view.resolvedStyle.width;
            var viewHeight = view.resolvedStyle.height;

            if (viewWidth <= 0f || viewHeight <= 0f)
                return Vector2.zero;

            var normalizedX = screenPosition.x / viewWidth;
            var normalizedY = screenPosition.y / viewHeight;

            var orthoSize = camera.orthographicSize;
            var aspect = camera.aspect;

            var worldX = (normalizedX - 0.5f) * orthoSize * aspect * 2f;
            var worldY = (0.5f - normalizedY) * orthoSize * 2f;

            var cameraPosition = camera.transform.localPosition;
            return new Vector2(cameraPosition.x + worldX, cameraPosition.y + worldY);
        }

        public void Dispose()
        {
            if (_view == null)
                return;

            _view.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            _view.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            _view.UnregisterCallback<MouseEnterEvent>(OnMouseEnter);
            _view.UnregisterCallback<MouseOutEvent>(OnMouseOut);
            _view.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            _view.UnregisterCallback<WheelEvent>(OnWheel);
        }

        private void OnWheel(WheelEvent evt)
        {
            var deltaTime = _deltaTimeProvider();
            var zoomSpeed = 18f * deltaTime;

            _targetCameraSize += zoomSpeed * evt.delta.y;
            _targetCameraSize = math.clamp(_targetCameraSize, 7f, 14f);

            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (_isWheelDown)
            {
                var deltaTime = _deltaTimeProvider();
                var moveSpeed = 4f * deltaTime;
                var delta = (evt.mousePosition - _previousMousePosition) * moveSpeed;
                delta.x *= -1f;

                _targetCameraPosition += new Vector3(delta.x, delta.y);
                _previousMousePosition = evt.mousePosition;
            }

            MouseMove?.Invoke(evt);
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button is 2 && _isWheelDown)
                _isWheelDown = false;

            MouseUp?.Invoke(evt);
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button is 2)
            {
                _isWheelDown = true;
                _previousMousePosition = evt.mousePosition;
            }

            MouseDown?.Invoke(evt);
            evt.StopPropagation();
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            MouseEnter?.Invoke(evt);
            evt.StopPropagation();
        }

        private void OnMouseOut(MouseOutEvent evt)
        {
            _isWheelDown = false;

            MouseOut?.Invoke(evt);
            evt.StopPropagation();
        }
    }
}
