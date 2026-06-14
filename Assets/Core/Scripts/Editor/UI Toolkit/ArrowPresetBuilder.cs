using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _.Scripts.Editor.UI_Toolkit;
using _.Scripts.Gameplay;
using _.Scripts.Utility.GameObject;
using DG.DOTweenEditor;
using DG.Tweening;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Path = System.IO.Path;
using ArrowNode = _.Scripts.Gameplay.SessionPreset.ArrowNode;

public class ArrowPresetBuilder : EditorWindow
{
    #region Private Fields
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    private ObjectField           m_DirectoryField      = default;
    private DropdownField         m_SelectField         = default;
    private Label                 m_CameraSizeLabel     = default;
    private Label                 m_CameraPositionLabel = default;
    private UIToolkitSceneControl m_SceneControl        = default;
    private VisualElement         m_Notify              = default;
    private Label                 m_NotifyLabel         = default;
    private VisualElement         m_Parameters          = default;
    private Button                m_OverlapToggleButton = default;
    private ObjectField           m_OverlapField        = default;
    private Image                 m_OverlapImage        = default;
    private Vector2Field          m_OverlapCenter       = default;
    private Vector2Field          m_OverlapScale        = default;

    private Dictionary<string, string> m_PresetNameToPath;
    private string                     m_DirectoryPath;
    private string                     m_PresetName;

    private bool   m_IsTrackingMouse;
    private float2 m_CurrentMousePosition;
    private Stack<int3> m_SelectedNodes;
    private int         m_PreviousSelectedNodesHash;
    private int3        m_HoveredGridIndex;
    private Action      m_EraseHoveredNodeAction;
    private int _initializeRecursionDepth;
    private const int _MaxInitIterations = 5;

    private Mesh                        m_CompositeMesh;
    private Material                    m_Material;
    private SpriteRenderer              m_SpriteRenderer;

    private ArrowDataManager             _dataManager;

    private Tween m_NotifyTween;
    private Tween m_ToggleTween;
    #endregion

    #region Events
    private event Action OnHoveredGridIndexChanged;
    #endregion

    #region Properties
    private float ActualCellSize => 1f;
    #endregion

    #region Public API
    [MenuItem("Window/UI Toolkit/ArrowPresetBuilder")]
    public static void ShowExample()
    {
        if (!HasSessionPresets())
        {
            UIToolkitModal.Open("Create first Session Preset", fileName =>
            {
                var sessionPreset = CreateSessionPresetInAssets(fileName);
                var assetsDirectory = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
                if (sessionPreset == null)
                    return;

                EditorApplication.delayCall += () => ShowExample(sessionPreset, assetsDirectory);
            });
            return;
        }

        ShowExample(null, null);
    }

    private static void ShowExample(SessionPreset initialPreset, DefaultAsset initialDirectory)
    {
        ArrowPresetBuilder wnd = GetWindow<ArrowPresetBuilder>();
        wnd.titleContent = new GUIContent("ArrowPresetBuilder");
        wnd.minSize = new Vector2(480, 720);
        wnd.maxSize = new Vector2(480, 720);

        if (initialPreset != null)
            EditorApplication.delayCall += () => wnd.SelectPreset(initialPreset, initialDirectory);
    }
    #endregion

    #region UI Toolkit Lifecycle
    private void OnDestroy()
    {
        ExportDataToAsset();
        _dataManager?.Dispose();
    }

    public void CreateGUI()
    {
        m_VisualTreeAsset.CloneTree(rootVisualElement);

        BindDirectoryUI();
        BindSceneControlUI();
        BindCameraUI();
        BindNotifyUI();
        BindOverlapUI();

        _dataManager = new ArrowDataManager();

        var directory = HasSessionPresets() ? FindFirstDirectoryWithPreset() : AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
        if (directory != null)
        {
            m_DirectoryField.SetValueWithoutNotify(directory);
            OnDirectoryChanged(directory);
        }

        EditorApplication.delayCall += RefreshSceneView;
    }

    private void BindDirectoryUI()
    {
        m_DirectoryField = rootVisualElement.Q<ObjectField>("DirectoryField");
        m_DirectoryField.RegisterValueChangedCallback(OnDirectoryChanged);

        m_SelectField = rootVisualElement.Q<DropdownField>("SelectField");
        m_SelectField.RegisterValueChangedCallback(OnPresetChanged);
    }

    private void BindSceneControlUI()
    {
        m_SceneControl = rootVisualElement.Q<UIToolkitSceneControl>();
        m_SceneControl.OnMouseEnterView += OnMouseEnterSceneControl;
        m_SceneControl.OnMouseOutView   += OnMouseOutSceneControl;
        m_SceneControl.OnMouseMoveView  += OnMouseMoveSceneControl;
        m_SceneControl.OnMouseDownView  += OnMouseDownSceneControl;
        m_SceneControl.OnMouseUpView    += OnMouseUpSceneControl;
    }

    private void BindCameraUI()
    {
        m_CameraSizeLabel      = rootVisualElement.Q<Label>("CameraSize");
        m_CameraSizeLabel.text = "";
        m_CameraSizeLabel.RegisterCallback<MouseUpEvent>(OnCameraSizeClicked);

        m_CameraPositionLabel      = rootVisualElement.Q<Label>("CameraPosition");
        m_CameraPositionLabel.text = "";
        m_CameraPositionLabel.RegisterCallback<MouseUpEvent>(OnCameraPositionClicked);
    }

    private void BindNotifyUI()
    {
        m_Notify      = rootVisualElement.Q<VisualElement>("Notify");
        m_NotifyLabel = rootVisualElement.Q<Label>("NotifyText");
    }

    private void BindOverlapUI()
    {
        m_Parameters                  =  rootVisualElement.Q<VisualElement>("Parameters");
        m_OverlapToggleButton         =  rootVisualElement.Q<Button>("ToggleButton");
        m_OverlapToggleButton.clicked += ToggleParameters;
        m_OverlapField                =  rootVisualElement.Q<ObjectField>("OverlapField");
        m_OverlapCenter               =  rootVisualElement.Q<Vector2Field>("OverlapCenter");
        m_OverlapScale                =  rootVisualElement.Q<Vector2Field>("OverlapScale");
        m_OverlapImage                =  rootVisualElement.Q<Image>("OverlapImage");
        m_OverlapField.RegisterValueChangedCallback(_ => OnOverlapChanged());
        m_OverlapCenter.RegisterValueChangedCallback(_ => OnOverlapChanged());
        m_OverlapScale.RegisterValueChangedCallback(_ => OnOverlapChanged());
    }
    #endregion

    #region Scene Interaction
    private void OnMouseUpSceneControl(MouseUpEvent evt)
    {
        if (evt.button is 0)
        {
            if (m_SelectedNodes is { Count: > 1 })
                ApplyArrow();

            m_SelectedNodes             = new Stack<int3>();

            OnHoveredGridIndexChanged -= ChangeNodeSelection;
        }

        if (evt.button is 1)
        {
            if (m_EraseHoveredNodeAction != null)
                OnHoveredGridIndexChanged -= m_EraseHoveredNodeAction;

            m_EraseHoveredNodeAction = null;
        }
    }

    private void OnMouseDownSceneControl(MouseDownEvent evt)
    {
        if (evt.button is 0)
        {
            m_SelectedNodes             = new Stack<int3>();

            OnHoveredGridIndexChanged += ChangeNodeSelection;
            ChangeNodeSelection();
        }

        if (evt.button is 1)
        {
            m_EraseHoveredNodeAction = () => _dataManager.RemoveNodeAt(m_HoveredGridIndex.xy);
            OnHoveredGridIndexChanged += m_EraseHoveredNodeAction;
        }
    }

    private void ApplyArrow()
    {
        if (!_dataManager.CanCreateArrow(m_SelectedNodes))
        {
            ShowNotify();
            return;
        }

        var path = m_SelectedNodes.Select(n => new int3(n.xy, 0)).ToList();
        _dataManager.AddArrowPath(path);

        m_SelectedNodes = new Stack<int3>();
    }

    private void ChangeNodeSelection()
    {
        if (m_SelectedNodes.Contains(m_HoveredGridIndex))
        {
            while (m_SelectedNodes.Count > 0 &&
                   m_SelectedNodes.TryPop(out int3 node) &&
                   !node.Equals(m_HoveredGridIndex)) ;
        }

        m_SelectedNodes.Push(m_HoveredGridIndex);
        RebuildCompositeSelectionMesh();
    }

    private void RebuildCompositeSelectionMesh()
    {
        if (m_CompositeMesh == null)
            m_CompositeMesh = new Mesh();
        else
            m_CompositeMesh.Clear();

        if (m_SelectedNodes == null || m_SelectedNodes.Count == 0)
        {
            m_CompositeMesh.RecalculateBounds();
            return;
        }

        var vertices  = new List<Vector3>();
        var triangles = new List<int>();
        var uvs       = new List<Vector2>();

        foreach (var node in m_SelectedNodes)
        {
            float3 start = (float3)node * ActualCellSize;

            Matrix4x4 matrix = Matrix4x4.TRS(start, Quaternion.identity, new Vector3(1, 1, 1));

            Vector3 v0 = new Vector3(-0.5f, -0.5f, 0);
            Vector3 v1 = new Vector3(0.5f,  -0.5f, 0);
            Vector3 v2 = new Vector3(-0.5f, 0.5f,  0);
            Vector3 v3 = new Vector3(0.5f,  0.5f,  0);

            int baseIndex = vertices.Count;
            vertices.Add(matrix.MultiplyPoint(v0));
            vertices.Add(matrix.MultiplyPoint(v1));
            vertices.Add(matrix.MultiplyPoint(v2));
            vertices.Add(matrix.MultiplyPoint(v3));

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 3);

            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 1));
            uvs.Add(new Vector2(1, 1));
        }

        m_CompositeMesh.SetVertices(vertices);
        m_CompositeMesh.SetTriangles(triangles, 0);
        m_CompositeMesh.SetUVs(0, uvs);
        m_CompositeMesh.RecalculateNormals();
        m_CompositeMesh.RecalculateBounds();
    }

    private void OnMouseMoveSceneControl(MouseMoveEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_CurrentMousePosition = evt.localMousePosition;

            UpdateHoveredGridIndex();
        }
    }

    private void UpdateHoveredGridIndex()
    {
        if (!GetHoveredGridIndex(out int3 gridIndex, out float3 point))
            return;

        int3 previous = m_HoveredGridIndex;
        m_HoveredGridIndex = gridIndex;

        if (!gridIndex.Equals(previous))
            OnHoveredGridIndexChanged?.Invoke();
    }

    private void OnMouseOutSceneControl(MouseOutEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_IsTrackingMouse          = false;

            m_EraseHoveredNodeAction = null;
            OnHoveredGridIndexChanged = delegate { };
        }
    }

    private void OnMouseEnterSceneControl(MouseEnterEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_IsTrackingMouse          = true;

            m_CurrentMousePosition    = evt.localMousePosition;
            m_EraseHoveredNodeAction = null;
            OnHoveredGridIndexChanged = delegate { };
        }
    }

    private void OnCameraPositionClicked(MouseUpEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_SceneControl.SetCameraPosition(Vector3.forward * -10);
        }
    }

    private void OnCameraSizeClicked(MouseUpEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_SceneControl.SetCameraSize(7);
        }
    }

    private void OnSceneUpdate()
    {
        if (m_SceneControl == null || _dataManager == null)
            return;

        UpdateCameraInfo();
        DrawCellUnderMouse();
        DrawSelectionPreview();
        DrawArrowOverlay();
    }

    private void UpdateCameraInfo()
    {
        if (m_SceneControl?.Camera == null)
            return;

        var zoom = m_SceneControl.Camera.orthographicSize / 14.0f;
        m_CameraSizeLabel.text = $"ZOOM: x{zoom:F2}";

        var localPosition = m_SceneControl.Camera.transform.localPosition;
        m_CameraPositionLabel.text = $"P: ({localPosition.x:F1}, {localPosition.y:F1})";
    }

    private void DrawCellUnderMouse()
    {
        if (GetHoveredGridIndex(out int3 index, out float3 point))
            m_SceneControl.DrawHandle(point, ActualCellSize, Color.lightBlue * new Vector4(1.0f, 1.0f, 1.0f, 0.2f));
    }

    private void DrawSelectionPreview()
    {
        if (!m_IsTrackingMouse || m_SelectedNodes is not {Count: > 0})
            return;

        if (m_SceneControl == null || m_CompositeMesh == null)
            return;

        m_SceneControl.DrawMesh(m_CompositeMesh, Matrix4x4.identity, Color.limeGreen * new Vector4(1.0f, 1.0f, 1.0f, 0.2f));
    }

    private void DrawArrowOverlay()
    {
        if (_dataManager == null)
            return;

        if (_dataManager.ComputeBuffer == null ||
            !_dataManager.ComputeBuffer.IsValid() ||
            _dataManager.ComputeBuffer.count == 0)
            return;

        if (m_Material == null || m_SceneControl?.Camera == null)
            return;

        float   height = m_SceneControl.Camera.orthographicSize * 2f;
        float   width  = height * m_SceneControl.Camera.aspect;
        Vector3 scale  = new Vector3(width, height, 1.0f);

        Vector3 translate = m_SceneControl.Camera.transform.position;
        translate.z = 0.0f;

        SceneDrawingUtility.DrawQuad(translate, Quaternion.identity, scale, m_SceneControl.Camera, m_Material);
    }

    private bool GetHoveredGridIndex(out int3 index, out float3 point)
    {
        if (m_SceneControl?.Camera == null)
        {
            index = int3.zero;
            point = (float3)index * ActualCellSize;
            return false;
        }

        var plane = new Plane(-Vector3.forward, Vector3.zero);
        var ray   = m_SceneControl.Camera.ScreenPointToRay(new float3(m_CurrentMousePosition, 0.0f));

        if (plane.Raycast(ray, out float distance))
        {
            var offset = ActualCellSize / 2.0f;
            point   =  (float3)ray.GetPoint(distance);
            point.y *= -1;

            point = math.round(point);

            index = (int3)(point / ActualCellSize);
            point = (float3)index * ActualCellSize;

            return true;
        }

        index = int3.zero;
        point = (float3)index * ActualCellSize;

        return false;
    }

    private void RefreshSceneView()
    {
        if (m_SceneControl == null)
            return;

        m_SceneControl.Grid = new GridParameters
        {
            color      = Color.gray3,
            lineSize   = 0.1f,
            fadeOutMin = 0.1f,
            fadeOutMax = 1f
        };

        m_SceneControl.Camera.orthographicSize = 7;

        m_Material = new Material(Shader.Find("Custom/ArrowsShader"));
        m_Material.SetColor("_BaseColor", new Color32(200, 200, 200, 255));
        m_Material.SetFloat("_ArrowOffset", 0.15f);
        m_Material.SetFloat("_ArrowSize", 0.5f);
        m_Material.SetVector("_ArrowShape", new Vector4(0.5f, 1.0f, 0.0f, 0.0f));
        m_Material.SetFloat("_ArrowCorner", 0.75f);
        m_Material.SetFloat("_TrailSize", 1.0f);
        m_Material.SetVector("_TrailCorner", new Vector4(1, 1, 1, 1));
        m_Material.SetInt("_ArrowsDataSize", 0);

        _dataManager.UpdateComputeBuffer(m_Material);

        m_SceneControl.OnUpdate(OnSceneUpdate);
    }
    #endregion

    #region Helper Methods
    private static bool HasSessionPresets()
    {
        var findAssets = AssetDatabase.FindAssets($"t:{nameof(SessionPreset)}");
        return findAssets != null && findAssets.Length != 0;
    }

    private static SessionPreset CreateSessionPresetInAssets(string fileName)
    {
        var sessionPreset = ScriptableObject.CreateInstance<SessionPreset>();
        sessionPreset.name = fileName;

        var path = $"Assets/{fileName}.asset";
        AssetDatabase.CreateAsset(sessionPreset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<SessionPreset>(path);
    }

    private void SelectPreset(SessionPreset preset, DefaultAsset directory)
    {
        if (preset == null)
            return;

        var presetPath = AssetDatabase.GetAssetPath(preset);
        if (string.IsNullOrEmpty(presetPath))
            return;

        var presetName = Path.GetFileNameWithoutExtension(presetPath);
        var directoryPath = directory != null ? AssetDatabase.GetAssetPath(directory) : GetAssetDirectory(presetPath);

        if (!string.IsNullOrEmpty(directoryPath))
        {
            var directoryAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(directoryPath);
            m_DirectoryField.SetValueWithoutNotify(directoryAsset);
            OnDirectoryChanged(directoryAsset);
        }

        m_PresetNameToPath ??= new Dictionary<string, string>();
        m_PresetNameToPath[presetName] = presetPath;

        if (!m_SelectField.choices.Contains(presetName))
            m_SelectField.choices = m_SelectField.choices.Append(presetName).ToList();

        m_SelectField.SetValueWithoutNotify(presetName);
        OnPresetChanged(presetName);
    }

    private static string GetAssetDirectory(string assetPath)
    {
        var lastSlashIndex = assetPath.LastIndexOf('/');
        return lastSlashIndex >= 0 ? assetPath.Substring(0, lastSlashIndex) : assetPath;
    }
#endregion
 
    #region Data Management
    private void ExportDataToAsset()
    {
        if (_dataManager == null || _dataManager.ArrowNodes == null || m_PresetNameToPath == null || string.IsNullOrEmpty(m_PresetName))
            return;

        if (m_PresetNameToPath.TryGetValue(m_PresetName, out string path))
        {
            var sessionPreset = AssetDatabase.LoadAssetAtPath<SessionPreset>(path);

            var valuesField = sessionPreset.GetType().GetField("_values", BindingFlags.NonPublic | BindingFlags.Instance);
            var boundsField = sessionPreset.GetType().GetField("_boundsSize", BindingFlags.NonPublic | BindingFlags.Instance);
            var nodes = _dataManager.ArrowNodes.ToArray();

            if (valuesField != null && nodes.Length > 0)
            {
                _dataManager.ValidateAndCleanPaths();
                valuesField.SetValue(sessionPreset, _dataManager.ArrowNodes.ToArray());

                int minX = nodes.Min(n => n.index.x);
                int maxX = nodes.Max(n => n.index.x);
                int minY = nodes.Min(n => n.index.y);
                int maxY = nodes.Max(n => n.index.y);
                int2 boundsSize = new int2(Mathf.Max(0, maxX - minX + 1), Mathf.Max(0, maxY - minY + 1));

                if (boundsField != null)
                    boundsField.SetValue(sessionPreset, boundsSize);
            }
            else if (valuesField != null && boundsField != null)
            {
                boundsField.SetValue(sessionPreset, new int2(0, 0));
            }
        }
    }

    private void OnPresetChanged(ChangeEvent<string> evt) => OnPresetChanged(evt.newValue);

    private void OnPresetChanged(string value)
    {
        if (_initializeRecursionDepth >= _MaxInitIterations)
            throw new InvalidOperationException($"ArrowPresetBuilder initialization exceeded {_MaxInitIterations} iterations. Fix the recursive preset/directory initialization chain.");

        string currentPresetName = m_PresetName;
        _initializeRecursionDepth++;

        try
        {
            if (!string.IsNullOrEmpty(currentPresetName))
                ExportDataToAsset();

            if (string.IsNullOrEmpty(value) || value == "== Create new ==")
            {
                if (m_PresetNameToPath == null || m_PresetNameToPath.Count == 0)
                {
                    m_PresetName = string.Empty;
                    return;
                }

                var sessionPreset = ScriptableObject.CreateInstance<SessionPreset>();

                UIToolkitModal.Open("Session Preset", (fileName) =>
                {
                    sessionPreset.name = fileName;
                    m_PresetName = sessionPreset.name;

                    var path = Path.Combine(m_DirectoryPath, fileName + ".asset");

                    AssetDatabase.CreateAsset(sessionPreset, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    m_PresetNameToPath ??= new Dictionary<string, string>();
                    m_PresetNameToPath[fileName] = path;

                    var updatedChoices = m_SelectField.choices
                        .Where(c => c != "== Create new ==")
                        .Concat(new[] { fileName, "== Create new ==" })
                        .ToList();
                    m_SelectField.choices = updatedChoices;
                    m_SelectField.value = fileName;
                });
            }
            else
            {
                m_PresetName = value;
            }

            if (m_PresetNameToPath == null || (!m_PresetNameToPath.ContainsKey(m_PresetName) && !string.IsNullOrEmpty(m_PresetName)))
            {
                return;
            }

            _dataManager.Clear();

            if (m_PresetNameToPath.TryGetValue(m_PresetName, out string path))
            {
                SessionPreset preset = AssetDatabase.LoadAssetAtPath<SessionPreset>(path);

                if (preset.Values is { Length: not 0 })
                {
                    _dataManager.LoadPreset(preset.Values);

                    if (m_Material != null)
                        _dataManager.UpdateComputeBuffer(m_Material);
                }
            }

            RefreshSceneView();
        }
        finally
        {
            _initializeRecursionDepth--;
        }
    }

    private void OnDirectoryChanged(ChangeEvent<Object> evt) => OnDirectoryChanged(evt.newValue);

    private void OnDirectoryChanged(Object value)
    {
        if (_initializeRecursionDepth >= _MaxInitIterations)
            throw new InvalidOperationException($"ArrowPresetBuilder initialization exceeded {_MaxInitIterations} iterations. Fix the recursive preset/directory initialization chain.");

        if (value == null)
        {
            m_DirectoryPath = null;
            m_PresetNameToPath = new Dictionary<string, string>();
            m_SelectField.choices = new List<string>() { "== Create new ==" };
            m_SelectField.value = "== Create new ==";
            return;
        }

        _initializeRecursionDepth++;

        try
        {
            m_DirectoryPath = AssetDatabase.GetAssetPath(value);

            if (string.IsNullOrEmpty(m_DirectoryPath))
            {
                m_PresetNameToPath = new Dictionary<string, string>();
                m_SelectField.choices = new List<string>() { "== Create new ==" };
                m_SelectField.value = "== Create new ==";
                return;
            }

            var presetGUIDs = AssetDatabase.FindAssets($"t:{nameof(SessionPreset)}", new[] { m_DirectoryPath });

            if (presetGUIDs is { Length: > 0 })
            {
                var presets = presetGUIDs.Select(AssetDatabase.GUIDToAssetPath).ToArray();

                m_PresetNameToPath = presets.ToDictionary(Path.GetFileNameWithoutExtension, v => v);

                m_SelectField.choices = presets.Select(Path.GetFileNameWithoutExtension).Append("== Create new ==").ToList();
                m_SelectField.value   = m_SelectField.choices[0];
            }
            else
            {
                m_PresetNameToPath = new Dictionary<string, string>();
                m_SelectField.choices = new List<string>() { "== Create new ==" };
                m_SelectField.value   = "== Create new ==";
            }
        }
        finally
        {
            _initializeRecursionDepth--;
        }
    }

    private Object FindFirstDirectoryWithPreset()
    {
        var findAssets = AssetDatabase.FindAssets($"t:{nameof(SessionPreset)}");

        if (findAssets != null && findAssets.Length != 0)
        {
            var first  = findAssets[0];
            var path   = AssetDatabase.GUIDToAssetPath(first);
            var directory = GetAssetDirectory(path);

            return AssetDatabase.LoadAssetAtPath(directory, typeof(DefaultAsset));
        }

        return AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
    }

    #endregion

    #region Private Methods
    private void OnOverlapChanged()
    {
        if (m_OverlapField.value == null)
        {
            if (m_SpriteRenderer != null)
                DestroyImmediate(m_SpriteRenderer.gameObject);
            return;
        }

        if (m_SpriteRenderer == null)
        {
            var gameObject = new GameObject("Overlap Renderer");
            gameObject.transform.position = new Vector3(0, 0, -1f);
            m_SpriteRenderer              = gameObject.AddComponent<SpriteRenderer>();

            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            material.SetFloat("_Surface", 1.0f);
            material.renderQueue = 3000;
            material.color = Color.white * new Vector4(1, 1, 1, 0.4f);

            m_SpriteRenderer.sharedMaterial = material;

            m_SceneControl?.AttachGameObjectToScene(gameObject);
        }

        var texture = m_OverlapField.value as Texture2D;
        var scale   = m_OverlapScale.value;
        var center  = m_OverlapCenter.value;

        var rect   = new Rect(Vector2.zero, new Vector2(texture.width, texture.height));
        var sprite = Sprite.Create(texture, rect, Vector2.one * 0.5f, 128f, 36);

        m_OverlapImage.sprite                 = sprite;
        m_SpriteRenderer.sprite               = sprite;
        m_SpriteRenderer.transform.position   = center;
        m_SpriteRenderer.transform.localScale = scale;
    }

    private void ShowNotify()
    {
        if (m_NotifyTween != null)
            return;

        m_NotifyLabel.text = "Arrow can`t being placed here!";
        var opacitySequence = DOTween.Sequence();

        opacitySequence.OnKill(() => m_NotifyTween = null);

        opacitySequence.Insert(0.0f, DOTween
            .To(
                () => m_Notify.style.opacity.value,
                (x) => m_Notify.style.opacity = x,
                1.0f,
                0.25f
            )
            .SetEase(Ease.OutQuad)
            .OnStart(() => m_Notify.style.display = DisplayStyle.Flex));

        var translate = m_Notify.style.translate.value;

        opacitySequence.Insert(0.0f, DOTween
            .To(
                () => m_Notify.style.translate.value.y.value,
                (y) => m_Notify.style.translate = new Translate(translate.x, y),
                translate.y.value,
                0.5f
            ))
            .SetEase(Ease.OutQuad)
            .OnStart(() => m_Notify.style.translate = new Translate(translate.x, translate.y.value - 50f));

        opacitySequence.Insert(0.5f, DOTween
            .To(
                () => m_Notify.style.opacity.value,
                (x) => m_Notify.style.opacity = x,
                0.0f,
                1.0f
            )
            .SetEase(Ease.InOutSine)
            .OnComplete(() => m_Notify.style.display = DisplayStyle.None));

        m_NotifyTween = opacitySequence;

        DOTweenEditorPreview.PrepareTweenForPreview(m_NotifyTween, false, false, true);
        DOTweenEditorPreview.Start();
    }

    private void ToggleParameters()
    {
        if (m_ToggleTween != null)
            return;

        var sequence = DOTween.Sequence();

        sequence.OnKill(() => m_ToggleTween = null);

        if (m_Parameters.style.translate.keyword is StyleKeyword.Null)
            m_Parameters.style.translate = m_Parameters.resolvedStyle.translate;

        Translate translate  = m_Parameters.style.translate.value;
        float     finalValue = translate.x == 0 ? -245 : 0;
        sequence.Insert(0.0f, DOTween
                .To(
                    () => m_Parameters.style.translate.value.x.value,
                    (x) => m_Parameters.style.translate = new Translate(x, translate.y, translate.z),
                    finalValue,
                    0.5f
                ))
                .SetEase(Ease.OutBounce);

        m_ToggleTween = sequence;

        DOTweenEditorPreview.PrepareTweenForPreview(m_ToggleTween, false, false, true);
        DOTweenEditorPreview.Start();
    }
    #endregion
}
