using System;
using System.Collections.Generic;
using System.Linq;
using _.Scripts.Editor.UI_Toolkit;
using _.Scripts.Gameplay;
using _.Scripts.Utility.Extensions;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

public class ArrowPresetBuilder : EditorWindow
{
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
    
    private ObjectField           m_DirectoryField = default;
    private DropdownField         m_SelectField    = default;
    private UIToolkitSceneControl m_SceneControl   = default;

    private Dictionary<string, string> m_PresetNameToPath;
    private string                     m_DirectoryPath;
    private string                     m_PresetName;
    private GameObject                 m_PreviewPlane;

    public void CreateGUI()
    {
        m_VisualTreeAsset.CloneTree(rootVisualElement);

        m_DirectoryField = rootVisualElement.Q<ObjectField>("DirectoryField");
        m_DirectoryField.RegisterValueChangedCallback(OnDirectoryChanged);
        
        m_SelectField = rootVisualElement.Q<DropdownField>("SelectField");
        m_SelectField.RegisterValueChangedCallback(OnPresetChanged);
        
        m_SceneControl = rootVisualElement.Q<UIToolkitSceneControl>();
        m_SceneControl.OnUpdate(OnSceneUpdate);
        
        m_DirectoryField.value = FindFirstDirectoryWithPreset();
        m_SelectField.value    = m_SelectField.choices[0];
        RefreshSceneView();
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
        
    }

    private void RefreshSceneView()
    {
        
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
