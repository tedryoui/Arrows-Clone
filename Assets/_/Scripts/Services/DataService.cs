using System;
using System.Collections.Generic;
using _.Scripts.Utility;

namespace _.Scripts.Services
{
    public class DataService
    {
#region Fields

        private Dictionary<string, IModel> _models;

#endregion

#region Properties

        public Dictionary<string, IModel> Models => _models;

#endregion
        
        public DataService()
        {
            _models = new Dictionary<string, IModel>();
        }

#region Model Manage

        public void Add(string identity, IModel model)
        {
            if (string.IsNullOrEmpty(identity))
                throw new ArgumentException("Identity cannot be null or empty.");
            
            if (!_models.TryAdd(identity, model))
                throw new KeyNotFoundException($"The model {identity} can not be added.");
            
            model.Reset();
        }

        public void Remove(string identity, out IModel model)
        {
            if (!_models.Remove(identity, out model))
                throw new KeyNotFoundException($"The model {identity} was not found.");
            
            model.Dispose();
        }
        
        public void Remove(string identity) => Remove(identity, out var model);

        public IModel Get(string identity)
        {
            if (string.IsNullOrEmpty(identity))
                throw new ArgumentException("Identity cannot be null or empty.");
            
            if (!_models.TryGetValue(identity, out var model))
                throw new KeyNotFoundException($"The model {identity} was not found.");
            
            return model;
        }

        public bool Has(string identity)
        {
            return _models.ContainsKey(identity);
        }

#endregion
    }
}