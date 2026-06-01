using _.Scripts.Utility.Debug;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _.Scripts.Entry_Point
{
    public class RootEntryPoint : IPostInitializable
    {
        [Inject] private IObjectResolver _objectResolver;
        
        public void PostInitialize()
        {
            var dataServiceDebugGameObject = new GameObject("DataServiceDebug");
            var component = dataServiceDebugGameObject.AddComponent<DataServiceDebug>();
            
            GameObject.DontDestroyOnLoad(dataServiceDebugGameObject);

            _objectResolver.Inject(component);
        }
    }
}