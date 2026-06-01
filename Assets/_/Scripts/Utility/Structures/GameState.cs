using System;
using _.Scripts.Services;

namespace _.Scripts.Utility.Structures
{
    public class GameState
    {
#region Structures

        public enum Variant
        {
            Runtime,
            Pause
        }

#endregion

#region Fields

        private Variant _value;

#endregion

#region Events

        private event Action<Variant> _onChanged;

#endregion

#region Properties

        public Variant Current => _value;

#endregion
        
        public GameState(Variant defaultState = Variant.Pause)
        {
            _onChanged = delegate { };
            
            _value = defaultState;
        }

#region Public API

        public void Subscribe(Action<Variant> callback)
        {
            _onChanged += callback;
        }

        public void Unsubscribe(Action<Variant> callback)
        {
            _onChanged -= callback;
        }

        public void Set(Variant value)
        {
            _value = value;
            
            Notify();
        }

        public void Notify()
        {
            _onChanged?.Invoke(Current);
        }

#endregion
    }
}