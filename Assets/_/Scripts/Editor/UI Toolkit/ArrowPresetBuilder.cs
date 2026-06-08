using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using _.Scripts.Editor.UI_Toolkit;
using _.Scripts.Gameplay;
using _.Scripts.Utility.Extensions;
using _.Scripts.Utility.GameObject;
using DG.DOTweenEditor;
using DG.Tweening;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

public class ArrowPresetBuilder : EditorWindow
{
    public struct SelectNode
    {
        public int3  current;
        public int3? next;

        public override bool Equals(object obj)
        {
            if (obj is SelectNode node) return Equals(node);
            return base.Equals(obj);
        }

        private bool Equals(SelectNode other)
        {
            return other.current.Equals(current);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(current);
        }
    }
    
    public class ArrowNode
    {
        public int2      index;
        public ArrowNode next;
        public ArrowNode previous;

        public float4 GetElementPoints()
        {
            return new float4(index, next?.index ?? float2.zero);
        }
        
        public ArrowElement.ElementType GetElementType()
        {
            return next == null ? ArrowElement.ElementType.Nip : ArrowElement.ElementType.Trail;
        }

        public ArrowElement.ElementDirection GetElementDirection(out bool negate)
        {
            if (next == null)
            {
                return previous.GetElementDirection(out negate);
            }
            else
            {
                int2 raw = (int2)math.normalize(next.index - index);
                return ArrowElement.ToDirection(raw, out negate);
            }
        }

        public List<ArrowNode> Flatten()
        {
            ArrowNode root = this;
            while (root.previous != null) root = root.previous;
            
            List<ArrowNode> elements = new List<ArrowNode>();
            while (root != null)
            {
                elements.Add(root);
                
                root = root.next;
            }

            return elements;
        }
    }
    
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("Window/UI Toolkit/ArrowPresetBuilder")]
    public static void ShowExample()
    {
        ArrowPresetBuilder wnd = GetWindow<ArrowPresetBuilder>();
        wnd.titleContent = new GUIContent("ArrowPresetBuilder");
        wnd.minSize      = new Vector2(480, 720);
        wnd.maxSize      = new Vector2(480, 720);
    }

    private void OnDestroy()
    {
        ExportDataToAsset();
    }

    private void ExportDataToAsset()
    {
        
    }

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

    private bool                        m_IsTrakingMouse;
    private float2                      m_CurrentMousePosition;
    private Stack<int3>                 m_SelectedNodes;
    private Mesh                        m_CompositeMesh;
    private int                         m_PreviousSelectedNodesHash;
    private List<ArrowNode>             m_ArrowNodes;
    private Dictionary<int2, ArrowNode> m_ArrowNodesMatrix;
    private List<ArrowElement>          m_ArrowElementsCache;
    private Material                    m_Material;
    private Mesh                        m_Mesh;
    private ComputeBuffer               m_ComputeBuffer;
    private int3                        m_HoveredGridIndex;
    private SpriteRenderer              m_SpriteRenderer;

    private event Action OnHoveredGridIndexChanged;

    private Tween m_NotifyTween;
    private Tween m_ToggleTween;
    
    private float ActualCellSize => 1f;

    public void CreateGUI()
        {
            m_VisualTreeAsset.CloneTree(rootVisualElement);

            m_DirectoryField = rootVisualElement.Q<ObjectField>("DirectoryField");
            m_DirectoryField.RegisterValueChangedCallback(OnDirectoryChanged);
            
            m_SelectField = rootVisualElement.Q<DropdownField>("SelectField");
            m_SelectField.RegisterValueChangedCallback(OnPresetChanged);
            
            m_SceneControl = rootVisualElement.Q<UIToolkitSceneControl>();
            m_SceneControl.OnMouseEnterView += OnMouseEnterSceneControl;
            m_SceneControl.OnMouseOutView   += OnMouseOutSceneControl;
            m_SceneControl.OnMouseMoveView  += OnMouseMoveSceneControl;
            m_SceneControl.OnMouseDownView  += OnMouseDownSceneControl;
            m_SceneControl.OnMouseUpView    += OnMouseUpSceneControl;
            
            m_CameraSizeLabel      = rootVisualElement.Q<Label>("CameraSize");
            m_CameraSizeLabel.text = "";
            m_CameraSizeLabel.RegisterCallback<MouseUpEvent>(OnCameraSizeClicked);
            
            m_CameraPositionLabel      = rootVisualElement.Q<Label>("CameraPosition");
            m_CameraPositionLabel.text = "";
            m_CameraPositionLabel.RegisterCallback<MouseUpEvent>(OnCameraPositionClicked);

            m_Notify      = rootVisualElement.Q<VisualElement>("Notify");
            m_NotifyLabel = rootVisualElement.Q<Label>("NotifyText");

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
            
            m_DirectoryField.value = FindFirstDirectoryWithPreset();
            m_SelectField.value    = m_SelectField.choices[0];
            
            EditorApplication.delayCall += RefreshSceneView;
        }

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
            
            m_SceneControl.AttachGameObjectToScene(gameObject);
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
            OnHoveredGridIndexChanged -= ProcessErasing;
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
            OnHoveredGridIndexChanged += ProcessErasing;
        }
    }

    private void ProcessErasing()
    {
        if (m_ArrowNodesMatrix.TryGetValue(m_HoveredGridIndex.xy, out var node))
        {
            if (node is { previous: not null, next: not null })
            {
                m_ArrowNodes.Add(node.next);

                ArrowNode left  = node.previous;
                ArrowNode right = node.next;
                
                node.previous.next = null;
                node.next.previous = null;
                node.previous      = null;
                node.next          = null;
                
                ValidateArrowNode(left);
                ValidateArrowNode(right);
            }
            
            if (node.previous == null &&
                node.next != null)
            {
                node.next.previous = null;
                
                m_ArrowNodes.Add(node.next);
                m_ArrowNodes.Remove(node);
                
                ValidateArrowNode(node.next);
            }

            if (node.next == null &&
                node.previous != null)
            {
                node.previous.next = null;
                
                ValidateArrowNode(node.previous);
            }
            
            m_ArrowNodesMatrix = m_ArrowNodes.SelectMany(x => x.Flatten()).ToDictionary(k => k.index, v => v);
            RefreshArrowElementsCache();
        
            m_Material.SetInt("_ArrowsDataSize", m_ArrowElementsCache.Count);
            
            if (m_ArrowElementsCache.Count > 0)
            {
                if (m_ComputeBuffer is not null && m_ComputeBuffer.IsValid())
                    m_ComputeBuffer.Dispose();

                m_ComputeBuffer = new ComputeBuffer(m_ArrowElementsCache.Count, ArrowElement.GetStrideSize());
                m_ComputeBuffer.SetData(m_ArrowElementsCache.ToArray());
                m_Material.SetBuffer("_ArrowsData", m_ComputeBuffer);
            }
        }
    }

    private void ValidateArrowNode(ArrowNode node)
    {
        ArrowNode root   = node;
        int       length = 0;

        if (root.previous != null)
            root = GetNodeRoot(root);

        ArrowNode temp = root;

        while (temp != null)
        {
            length++;
            temp = temp.next;
        }

        if (length <= 1)
            m_ArrowNodes.Remove(root);
    }

    private ArrowNode GetNodeRoot(ArrowNode root)
    {
        ArrowNode node = root;
        while (node.previous != null)
            node = node.previous;
        return node;
    }

    private void ApplyArrow()
    {
        if (!CanArrowBeingCreated())
        {
            ShowNotify();
            return;
        }

        ArrowNode node     = default;
        int3      index = m_SelectedNodes.Pop();

        node = new ArrowNode
        {
            index    = index.xy,
            next     = null,
            previous = null
        };

        while (m_SelectedNodes.TryPop(out index))
        {
            ArrowNode temp = new ArrowNode
            {
                index    = index.xy,
                next     = node,
                previous = null
            };

            node.previous = temp;
            node          = temp;
        }

        m_ArrowNodes ??= new List<ArrowNode>();
        
        m_ArrowNodes.Add(node);
        m_ArrowNodesMatrix = m_ArrowNodes.SelectMany(x => x.Flatten()).ToDictionary(k => k.index, v => v);
        RefreshArrowElementsCache();
        
        if (m_ComputeBuffer is not null && m_ComputeBuffer.IsValid())
            m_ComputeBuffer.Dispose();
        
        m_ComputeBuffer = new ComputeBuffer(m_ArrowElementsCache.Count, ArrowElement.GetStrideSize());
        m_ComputeBuffer.SetData(m_ArrowElementsCache.ToArray());
        m_Material.SetInt("_ArrowsDataSize", m_ComputeBuffer.count);
        m_Material.SetBuffer("_ArrowsData", m_ComputeBuffer);
    }

    private void RefreshArrowElementsCache()
    {
        m_ArrowElementsCache = m_ArrowNodes.SelectMany(x => x.Flatten()).Select(x => new ArrowElement
        {
            elementType            = x.GetElementType(),
            elementDirection       = x.GetElementDirection(out bool negate),
            elementDirectionNegate = negate ? 1 : 0,
            elementPoints          = x.GetElementPoints()
        }).ToList();
    }

    private bool CanArrowBeingCreated()
    {
        if (m_ArrowNodesMatrix == null)
            return true;
        
        foreach (var node in m_SelectedNodes)
        {
            bool registered = m_ArrowNodesMatrix.ContainsKey(node.xy);

            if (registered)
                return false;
        }

        return true;
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
            m_IsTrakingMouse          = false;
            
            OnHoveredGridIndexChanged = delegate { };
        }
    }

    private void OnMouseEnterSceneControl(MouseEnterEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_IsTrakingMouse          = true;
            
            m_CurrentMousePosition    = evt.localMousePosition;
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

    private void OnPresetChanged(ChangeEvent<string> evt)
    {
        if (!string.IsNullOrEmpty(m_PresetName))
            ExportDataToAsset();
        
        if (evt.newValue.Equals("== Create new =="))
        {
            var sessionPreset = ScriptableObject.CreateInstance<SessionPreset>();

            UIToolkitModal.Open("Session Preset", (fileName) =>
            {
                sessionPreset.name = fileName;
                m_PresetName = sessionPreset.name;
                
                var path = Path.Combine(m_DirectoryPath, fileName + ".asset");
                
                AssetDatabase.CreateAsset(sessionPreset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                OnDirectoryChanged(m_DirectoryField.value);

                if (m_SelectField.choices.Contains(m_PresetName))
                    m_SelectField.value = m_SelectField.choices[m_SelectField.choices.IndexOf(m_PresetName)];
                else 
                    m_SelectField.value = m_SelectField.choices[0];
            });
        }
        else
        {
            m_PresetName = evt.newValue;
        }

        RefreshSceneView();
    }

    private void OnSceneUpdate()
     {
         var zoom = m_SceneControl.Camera.orthographicSize / 14.0f;
         m_CameraSizeLabel.text = $"ZOOM: x{zoom:F2}";
         
         var localPosition = m_SceneControl.Camera.transform.localPosition;
         m_CameraPositionLabel.text = $"P: ({localPosition.x:F1}, {localPosition.y:F1})";

         if (m_IsTrakingMouse)
             DrawCellUnderMouse();

         if (m_IsTrakingMouse && m_SelectedNodes is {Count: > 0})
             m_SceneControl.DrawMesh(m_CompositeMesh, Matrix4x4.identity, Color.limeGreen * new Vector4(1.0f, 1.0f, 1.0f, 0.2f));

         if (m_ComputeBuffer != null && 
             m_ComputeBuffer.IsValid() && 
             m_ComputeBuffer.count > 0)
         {
             float   height = m_SceneControl.Camera.orthographicSize * 2f;
             float   width  = height * m_SceneControl.Camera.aspect;
             Vector3 scale  = new Vector3(width, height, 1.0f);
             
             Vector3 translate = m_SceneControl.Camera.transform.position;
             translate.z = 0.0f;
             
             m_SceneControl.DrawMesh(m_Mesh, Matrix4x4.TRS(translate, Quaternion.identity, scale),
                 m_Material);
         }
     }

    private void DrawCellUnderMouse()
    {
        if (GetHoveredGridIndex(out int3 index, out float3 point)) 
            m_SceneControl.DrawHandle(point, ActualCellSize, Color.lightBlue * new Vector4(1.0f, 1.0f, 1.0f, 0.2f));
    }

    private bool GetHoveredGridIndex(out int3 index, out float3 point)
    {
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

        m_Mesh = GetQuadMesh();

        m_SceneControl.OnUpdate(OnSceneUpdate);
    }

    private Mesh GetQuadMesh()
    {
        var mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f,  -0.5f, 0),
            new Vector3(-0.5f, 0.5f,  0),
            new Vector3(0.5f,  0.5f,  0)
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
        return mesh;
    }

    private void OnDirectoryChanged(ChangeEvent<Object> evt) => OnDirectoryChanged(evt.newValue);

    private void OnDirectoryChanged(Object value)
    {
        m_DirectoryPath = AssetDatabase.GetAssetPath(value);

        var presetGUIDs = AssetDatabase.FindAssets($"t:{nameof(SessionPreset)}", new[] { m_DirectoryPath });

        if (presetGUIDs is { Length: > 0 })
        {
            var presets = presetGUIDs.Select(AssetDatabase.GUIDToAssetPath).ToArray();
            
            m_PresetNameToPath = presets.ToDictionary(Path.GetFileNameWithoutExtension, v => v);
        
            m_SelectField.SetEnabled(true);
            
            m_SelectField.choices = presets.Select(Path.GetFileNameWithoutExtension).Append("== Create new ==").ToList();
            m_SelectField.value   = m_SelectField.choices[0];
        }
        else
        {
            m_SelectField.SetEnabled(false);

            m_SelectField.choices = new List<string>() { "" };
            m_SelectField.value   = m_SelectField.choices[0];
        }
    }

    private Object FindFirstDirectoryWithPreset()
    {
        var findAssets = AssetDatabase.FindAssets($"t:{nameof(SessionPreset)}");

        if (findAssets != null && findAssets.Length != 0)
        {
            var first     = findAssets[0];
            var directory = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(first));
            
            return AssetDatabase.LoadAssetAtPath(directory,  typeof(DefaultAsset));
        }

        return null;
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
}
