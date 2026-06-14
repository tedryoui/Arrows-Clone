using System;
using _.Scripts.Utility;
using UnityEngine;

namespace _.Scripts.Models
{
    [Serializable]
    public class SessionModel : IModel
    {
        [SerializeField] private int _heartCount;
        [SerializeField] private int _totalSeconds;
        [SerializeField] private int _currentHeartCount;
        [SerializeField] private int _currentSeconds;

        public int HeartCount
        {
            get => _heartCount;
            set => _heartCount = value;
        }

        public int TotalSeconds
        {
            get => _totalSeconds;
            set => _totalSeconds = value;
        }

        public int CurrentHeartCount
        {
            get => _currentHeartCount;
            set => _currentHeartCount = value;
        }

        public int CurrentSeconds
        {
            get => _currentSeconds;
            set => _currentSeconds = value;
        }

        public void Reset()
        {
            _heartCount = 0;
            _totalSeconds = 0;
            
            _currentHeartCount = 0;
            _currentSeconds = 0;
        }

        public void Dispose()
        {
            
        }
    }
}