using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _.Scripts.Utility.Structures
{
    public class Timer
    {

#region Fields

        private float _totalSeconds;
        private float _currentSeconds;
        
        private bool _isPaused;
        private CancellationToken _cancellationToken;

#endregion

#region Events

        public event Action        OnPause;
        public event Action        OnResume;
        public event Action        OnComplete;
        public event Action<float> OnProceed;

#endregion

#region Properties

        public float  Percentage => (_totalSeconds - _currentSeconds) / _totalSeconds;
        public double Seconds    => _currentSeconds;

#endregion
        
        public Timer(
            float totalSeconds, 
            CancellationToken cancellationToken = default, 
            bool autoPlay = false)
        {
            _cancellationToken = cancellationToken;
            
            _totalSeconds  = totalSeconds;
            _currentSeconds = 0f;
            _isPaused      = true;
            
            OnPause    = delegate { };
            OnResume   = delegate { };
            OnComplete = delegate { };
            OnProceed  = delegate { };
            
            if (autoPlay)
                Run(_cancellationToken).Forget();
        }

#region Private Methods

        private async UniTaskVoid Run(CancellationToken cancellationToken = default)
                {
                    _currentSeconds = _totalSeconds;
                    _isPaused      = false;
        
                    while (_currentSeconds >= 0f)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        
                        await UniTask.Yield(cancellationToken, cancelImmediately: true);
                        
                        if (!_isPaused)
                            Reduce();
                    }
                    
                    if (!cancellationToken.IsCancellationRequested)
                        OnComplete?.Invoke();
                }
        
        private void Reduce()
        {
            _currentSeconds -= Time.deltaTime;
            
            OnProceed?.Invoke(Percentage);
        }

#endregion

#region Public API

        public void Run() => Run(_cancellationToken).Forget();

        public void Pause(bool value)
        {
            _isPaused = value;
            
            if (value)
                OnPause?.Invoke();
            else
                OnResume?.Invoke();
        }

#endregion
    }
}