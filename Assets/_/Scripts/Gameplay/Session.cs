using System;
using System.Threading;
using _.Scripts.Models;
using UnityEngine;
using Timer = _.Scripts.Utility.Structures.Timer;

namespace _.Scripts.Gameplay
{
    [Serializable]
    public class Session
    {
        private SessionModel _sessionModel;
        
        private Timer _timer;

        private bool _isRaycastBlocked;

        private CancellationTokenSource _sessionCancellationTokenSource;
        
        public Timer Timer => _timer;

        private event Action _onLose;
        private event Action _onWin;
        
        public Session(SessionModel sessionModel)
        {
            _sessionModel = sessionModel;
            _sessionCancellationTokenSource = CancellationTokenSource
                .CreateLinkedTokenSource(
                    Application.exitCancellationToken);

            _isRaycastBlocked = false;
            
            _onLose = delegate { };
            _onWin  = delegate { };
            
            BuildTimer();
        }

#region Private Methods

#region Timer Manage

        private void BuildTimer()
        {
            _timer = new Timer(_sessionModel.TotalSeconds, _sessionCancellationTokenSource.Token);

            _timer.OnComplete += TimerOnComplete;

            _timer.Run();
        }

        private void TimerOnComplete()
        {
            _onLose?.Invoke();
        }
        
#endregion

#endregion

#region Public API

        public void End()
        {
            _sessionCancellationTokenSource?.Cancel();
        }

        public void OnLose(Action callback)
        {
            _onLose = callback;
        }

        public void OnWin(Action callback)
        {
            _onWin  = callback;
        }

#endregion
    }
}