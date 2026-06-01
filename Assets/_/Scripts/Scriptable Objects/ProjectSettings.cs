using System;
using System.Collections.Generic;
using System.Linq;
using _.Scriptable_Objects;
using _.Scripts.User_Interface.Gameplay_Screen;
using _.Scripts.Utility;
using _.Scripts.Utility.Structures;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace _.Scriptable_Objects
{
    [UnityEngine.CreateAssetMenu(fileName = "Project Settings", menuName = "Arrows Clone/Project Settings", order = 0)]
    public class ProjectSettings : ScriptableObject
    {
        [SerializeField] private AssetReferenceGameObject _mainCameraAssetReference;
        
        [SerializeField] private CachedIdentityArrayT<UserInterfacePreset> _userInterfacePresets;
        
        public IReadOnlyDictionary<string, UserInterfacePreset> UserInterfacePresets => _userInterfacePresets.Values;
    }
}

[Serializable, InlineProperty, HideLabel]
public struct UserInterfacePreset
{
#if UNITY_EDITOR
    [OnValueChanged("UpdateOtherAssemblyNames")]
    [ValueDropdown("FriendlyUserInterfaceAssemblyNames")]
#endif
    public string UserInterfaceAssemblyName;
    [ReadOnly] 
    public string UserInterfaceViewAssemblyName;
    public AssetReferenceGameObject GameObjectAssetReference;
    [ReadOnly]
    public string UserInterfaceAnimationAssemblyName;
    [InlineButton("CreateSuitablePreset", label: "Create")]
    public AnimationPreset          AnimationPreset;
    
#if UNITY_EDITOR

    private void UpdateOtherAssemblyNames()
    {
        if (string.IsNullOrEmpty(UserInterfaceAssemblyName))
            throw new ArgumentException("UserInterfaceAssemblyName cannot be null or empty");
        
        var userInterfaceType = System.Type.GetType(UserInterfaceAssemblyName);
        
        if (userInterfaceType == null)
            throw new ArgumentException($"UserInterfaceAssemblyName '{UserInterfaceAssemblyName}' not found");
        
        if (userInterfaceType.BaseType == null)
            throw new ArgumentException($"UserInterfaceAssemblyName '{UserInterfaceAssemblyName}' not found");
        
        var animationType     = userInterfaceType.BaseType.GetGenericArguments()[1];
        
        if (animationType == null)
            throw new ArgumentException($"UserInterfaceAnimation '{userInterfaceType.GetGenericArguments()[1].AssemblyQualifiedName}' not found");

        UserInterfaceAnimationAssemblyName = animationType.AssemblyQualifiedName;

        var viewType = userInterfaceType.BaseType.GetGenericArguments()[0];
        
        if (viewType == null)
            throw new ArgumentException($"UserInterfaceView '{userInterfaceType.GetGenericArguments()[0].AssemblyQualifiedName}' not found");
        
        UserInterfaceViewAssemblyName = viewType.AssemblyQualifiedName;
    }
    
    private ValueDropdownList<string> FriendlyUserInterfaceAssemblyNames()
    {
        var types = TypeCache.GetTypesDerivedFrom<UserInterface>();
        var list = new ValueDropdownList<string>();
        
        foreach (var type in types.Where(x => !x.IsAbstract))
            list.Add(type.Name, type.AssemblyQualifiedName);

        return list;
    }

    private void CreateSuitablePreset()
    {
        if (string.IsNullOrEmpty(UserInterfaceAssemblyName))
            throw new ArgumentException("UserInterfaceAssemblyName cannot be null or empty");
        
        var userInterfaceType = System.Type.GetType(UserInterfaceAssemblyName);
        
        if (userInterfaceType == null)
            throw new ArgumentException($"UserInterfaceAssemblyName '{UserInterfaceAssemblyName}' not found");
        
        if (userInterfaceType.BaseType == null)
            throw new ArgumentException($"UserInterfaceAssemblyName '{UserInterfaceAssemblyName}' not found");
        
        var animationType     = userInterfaceType.BaseType.GetGenericArguments()[1];
        
        if (animationType == null)
            throw new ArgumentException($"UserInterfaceAnimation '{userInterfaceType.GetGenericArguments()[1].AssemblyQualifiedName}' not found");
        
        if (animationType.BaseType == null)
            throw new ArgumentException($"UserInterfaceAnimation '{userInterfaceType.GetGenericArguments()[1].AssemblyQualifiedName}' not found");
        
        var presetType        = animationType.BaseType.GetGenericArguments()[0];
        
        if (presetType == null)
            throw new ArgumentException($"AnimationPreset '{animationType.GetGenericArguments()[0].AssemblyQualifiedName}' not found");

        AnimationPreset = ScriptableObject.CreateInstance(presetType) as AnimationPreset;
        AssetDatabase.CreateAsset(AnimationPreset, "Assets/" + presetType.Name + ".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
#endif 
}