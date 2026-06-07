using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using _.Scripts.Editor.UI_Toolkit;
using _.Scripts.Gameplay;
using _.Scripts.Utility.Extensions;
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
        public int3 current;
        public int3 next;

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
    
    private ObjectField           m_DirectoryField      = default;
    private DropdownField         m_SelectField         = default;
    private Label                 m_CameraSizeLabel     = default;
    private Label                 m_CameraPositionLabel = default;
    private UIToolkitSceneControl m_SceneControl        = default;

    private Dictionary<string, string> m_PresetNameToPath;
    private string                     m_DirectoryPath;
    private string                     m_PresetName;

    private bool              m_IsTrakingMouse;
    private bool              m_IsTrackingSelection;
    private float2            m_CurrentMousePosition;
    private int3?             m_PreviousSelectedGridIndex;
    private Stack<SelectNode> m_SelectedNodes;
    
    private float ActualCellSize => m_SceneControl.Camera.orthographicSize switch
    {
        var value when value <= 14.0f => 1f,
        var value when value <= 28.0f => 2f,
        var value when value <= 56.0f => 4f
    };

    public void CreateGUI()
        {
            m_VisualTreeAsset.CloneTree(rootVisualElement);

            m_DirectoryField = rootVisualElement.Q<ObjectField>("DirectoryField");
            m_DirectoryField.RegisterValueChangedCallback(OnDirectoryChanged);
            
            m_SelectField = rootVisualElement.Q<DropdownField>("SelectField");
            m_SelectField.RegisterValueChangedCallback(OnPresetChanged);
            
            m_SceneControl = rootVisualElement.Q<UIToolkitSceneControl>();
            m_SceneControl.OnUpdate(OnSceneUpdate);
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
            
            m_DirectoryField.value = FindFirstDirectoryWithPreset();
            m_SelectField.value    = m_SelectField.choices[0];
            
            // Delay RefreshSceneView to ensure scene is initialized
            EditorApplication.delayCall += RefreshSceneView;
        }

    private void OnMouseUpSceneControl(MouseUpEvent evt)
    {
        if (evt.button is 0 && m_IsTrackingSelection)
        {
            m_IsTrackingSelection = false;
            
            m_SelectedNodes             = new Stack<SelectNode>();
            m_PreviousSelectedGridIndex = null;
        }
    }

    private void OnMouseDownSceneControl(MouseDownEvent evt)
    {
        if (evt.button is 0 && !m_IsTrackingSelection)
        {
            m_IsTrackingSelection = true;
            
            m_SelectedNodes             = new Stack<SelectNode>();
            m_PreviousSelectedGridIndex = null;
        }
    }

    private void OnMouseMoveSceneControl(MouseMoveEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_CurrentMousePosition = evt.mousePosition;
        }
    }

    private void OnMouseOutSceneControl(MouseOutEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_IsTrakingMouse = false;
        }
    }

    private void OnMouseEnterSceneControl(MouseEnterEvent evt)
    {
        if (m_SceneControl.IsValid())
        {
            m_IsTrakingMouse       = true;
            m_CurrentMousePosition = evt.mousePosition;
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

         if (m_IsTrackingSelection && m_IsTrakingMouse)
             DrawSelection();
     }

    private void DrawSelection()
    {   
        foreach (var node in m_SelectedNodes)
        {
            float3 snapPoint = (float3)node.current * ActualCellSize;
            
            m_SceneControl.DrawHandle(snapPoint, ActualCellSize, Color.limeGreen * new Vector4(1.0f , 1.0f, 1.0f, 0.2f));
        }
        
        if (!GetHoveredGridIndex(out int3 index, out float3 point)) return;

        if (m_SelectedNodes.Count == 0 && m_PreviousSelectedGridIndex == null)
            m_PreviousSelectedGridIndex = index;

        int3 previous = m_PreviousSelectedGridIndex!.Value;
        if (index.Equals(previous))
            return;

        TryAddGridIndex(index, previous);
    }

    private void TryAddGridIndex(int3 index, int3 previous)
    {
        if (m_SelectedNodes.Count == 0)
        {
            m_SelectedNodes.Push(new SelectNode
            {
                current = previous,
                next    = index
            });
            m_PreviousSelectedGridIndex = index;

            return;
        }

        var search = new SelectNode() { current = index };
        if (m_SelectedNodes.Contains(search))
        {
            SelectNode temp = m_SelectedNodes.Pop();

            while (!temp.Equals(search))
            {
                temp = m_SelectedNodes.Pop();
            }

            m_PreviousSelectedGridIndex = index;
        }
        else
        {
            m_SelectedNodes.Push(new SelectNode()
            {
                current = previous,
                next    = index
            });

            m_PreviousSelectedGridIndex = index;
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
        var ray   = m_SceneControl.Camera.ScreenPointToRay((Vector3)new float3(m_CurrentMousePosition, 0.0f));

        if (plane.Raycast(ray, out float distance))
        {
            var offset = ActualCellSize / 2.0f;
            point  = (float3)ray.GetPoint(distance);

            if (point.x >= 0.0f) point.x += offset;
            else point.x                 -= offset;

            point.y *= -1;
            if (point.y >= 0.0f) point.y += offset * 2.0f;
            
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
}
