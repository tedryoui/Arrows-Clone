using System;
using _.Scripts.Models;
using _.Scripts.Services;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace _.Scripts.Utility.Debug
{
    public class DataServiceDebug : MonoBehaviour
    {
        [ReadOnly] public SessionModel SessionModel;

        [Inject]
        private void Configure(DataService dataService)
        {
            if (dataService.Has(MODEL_IDENTITIES.SESSION_MODEL))
                SessionModel = dataService.Get(MODEL_IDENTITIES.SESSION_MODEL) as SessionModel;
        }
    }
}