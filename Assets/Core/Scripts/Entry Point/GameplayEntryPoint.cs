using System.Linq;
using _.Scriptable_Objects;
using _.Scripts.Gameplay;
using _.Scripts.Models;
using _.Scripts.Services;
using _.Scripts.User_Interface.Gameplay_Screen;
using _.Scripts.Utility;
using _.Scripts.Utility.Structures;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace _.Scripts.Entry_Point
{
    public class GameplayEntryPoint : IPostInitializable
    {
        [Inject] private IObjectResolver _objectResolver;
        [Inject] private ProjectSettings _projectSettings;
        [Inject] private GameplayService _gameplayService;
        [Inject] private DataService     _dataService;

        public void PostInitialize()
        {
            var uiCreationOperation = CreateUI<GameplayScreen>(UI_IDENTITIES.GAMEPLAY_UI).GetAwaiter();
            uiCreationOperation.OnCompleted(() =>
            {
                _gameplayService.GameState.Set(GameState.Variant.Runtime);
            });
            
            
            var sessionModel = _dataService.Get(MODEL_IDENTITIES.SESSION_MODEL) as SessionModel;

            sessionModel.Reset();
            sessionModel.TotalSeconds = 10;
            
            var session      = new Session(sessionModel);
            session.Timer.OnProceed += (value) => sessionModel.CurrentSeconds = (int)(value * sessionModel.TotalSeconds);
            
            _gameplayService.StartSession(session);
            _gameplayService.GameState.Subscribe(OnGameStateChanged);
            _gameplayService.GameState.Set(GameState.Variant.Pause);
        }

        private async UniTask<T> CreateUI<T>(string gameplayUI)
        where T : UserInterface
        {
            var gameplayUIPreset = _projectSettings.UserInterfacePresets[UI_IDENTITIES.GAMEPLAY_UI];
            
            var loadAssetAsync = await gameplayUIPreset.GameObjectAssetReference.LoadAssetAsync<GameObject>();
            
            var gameObject = (await Object.InstantiateAsync(loadAssetAsync)).First();
            var component  = gameObject.GetComponent<UIDocument>();

            var animation = System.Activator
                .CreateInstance(System.Type.GetType(gameplayUIPreset.UserInterfaceAnimationAssemblyName),
                    args: gameplayUIPreset.AnimationPreset);
            var view = System.Activator
                .CreateInstance(System.Type.GetType(gameplayUIPreset.UserInterfaceViewAssemblyName), args: component);
            var userInterface = System.Activator
                .CreateInstance(System.Type.GetType(gameplayUIPreset.UserInterfaceAssemblyName), args: new object[] { view, animation })
                as T;
            
            _objectResolver.Inject(userInterface);
            
            return userInterface;
        }

        private void OnGameStateChanged(GameState.Variant currentState)
        {
            _gameplayService.Session.Timer.Pause(currentState is GameState.Variant.Pause);
        }
    }
}