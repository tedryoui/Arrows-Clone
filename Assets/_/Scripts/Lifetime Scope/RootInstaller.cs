using _.Scriptable_Objects;
using _.Scripts.Entry_Point;
using _.Scripts.Models;
using _.Scripts.Services;
using _.Scripts.Utility;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _.Scripts.Lifetime_Scope
{
    public class RootInstaller : LifetimeScope
    {
        [SerializeField] private ProjectSettings _projectSettings;
        
        protected override void Configure(IContainerBuilder builder)
        {
            var dataService = new DataService();
            
            dataService.Add(MODEL_IDENTITIES.SESSION_MODEL, new SessionModel());

            builder.RegisterInstance<ProjectSettings>(_projectSettings).AsSelf();
            builder.RegisterInstance<DataService>(dataService).AsSelf();
            
            builder.RegisterEntryPoint<RootEntryPoint>();
        }
    }
}