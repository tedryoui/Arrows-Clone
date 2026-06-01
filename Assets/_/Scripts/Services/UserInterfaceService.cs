using System;
using System.Collections.Generic;
using _.Scripts.Utility;
using VContainer;

namespace _.Scripts.Services
{
    public class UserInterfaceService
    {
        [Inject] private IObjectResolver _objectResolver;
        
        private Dictionary<string, UserInterface> _userInterfaces;

        public UserInterfaceService()
        {
            _userInterfaces = new Dictionary<string, UserInterface>();
        }

#region UI Manage

        public void Add(string identity, UserInterface userInterface)
        {
            if (string.IsNullOrEmpty(identity))
                throw new ArgumentException($"Invalid identity: {identity}");
            
            if (!_userInterfaces.TryAdd(identity, userInterface))
                throw new Exception($"UserInterface with identity {identity} already exists.");
            
            _objectResolver.Inject(userInterface);
            userInterface.Hide(false);
        }

        public void Remove(string identity) => Remove(identity, out var _);

        public void Remove(string identity, out UserInterface userInterface)
        {
            if (string.IsNullOrEmpty(identity))
                throw new ArgumentException($"Invalid identity: {identity}");
            
            if (!_userInterfaces.Remove(identity, out userInterface))
                throw new KeyNotFoundException($"UserInterface with identity {identity} was not found.");
        }

        public UserInterface Get(string identity)
        {
            if (string.IsNullOrEmpty(identity))
                throw new ArgumentException($"Invalid identity: {identity}");
            
            if (!_userInterfaces.TryGetValue(identity, out UserInterface userInterface))
                throw new KeyNotFoundException($"UserInterface with identity {identity} was not found.");
            
            return userInterface;
        }

        public bool Has(string identity)
        {
            return _userInterfaces.ContainsKey(identity);
        }

#endregion
    }
}