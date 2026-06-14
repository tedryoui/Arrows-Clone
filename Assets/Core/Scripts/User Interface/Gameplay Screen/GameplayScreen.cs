using System;
using _.Scripts.Services;
using _.Scripts.Utility;
using UnityEngine;
using UnityEngine.Rendering.UI;
using VContainer;

namespace _.Scripts.User_Interface.Gameplay_Screen
{
    public class GameplayScreen : UserInterface<GameplayScreenView, GameplayScreenAnimations>
    {
        [Inject] private GameplayService _gameplayService;

        [Inject]
        private void Configure()
        {
            Bind();
        }
        
        private int _lastTotalSeconds;
        
        public GameplayScreen(GameplayScreenView view, GameplayScreenAnimations animations) : base(view, animations)
        {
            _lastTotalSeconds = int.MaxValue;
        }

        protected override void Bind()
        {
            _gameplayService.Session.Timer.OnProceed += OnTimerChanged;
        }

        private void OnTimerChanged(float percentage)
        {
            bool isSecondChanged = (int)_gameplayService.Session.Timer.Seconds != _lastTotalSeconds;
            
            if (isSecondChanged)
            {
                var timeSpan = TimeSpan.FromSeconds(_gameplayService.Session.Timer.Seconds);
                
                View.ProgressBar.value = (1.0f - percentage) * 100f;
                View.ProgressBar.title = $"{timeSpan:mm}m {timeSpan:ss}s";

                _lastTotalSeconds = (int)_gameplayService.Session.Timer.Seconds;
            }
        }

        public override void Show(bool animate)
        {
            
        }

        public override void Hide(bool animate)
        {
            
        }
    }
}