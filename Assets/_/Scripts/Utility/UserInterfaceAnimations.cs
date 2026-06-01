using _.Scriptable_Objects;
using UnityEngine;

namespace _.Scripts.Utility
{
    public abstract class UserInterfaceAnimations
    {
        protected abstract AnimationPreset Preset { get; }
    }
    
    public abstract class UserInterfaceAnimations<TAnimationPreset> :  UserInterfaceAnimations
    where TAnimationPreset : AnimationPreset
    {
        private AnimationPreset _preset;
        
        protected override AnimationPreset Preset => _preset;
        
        public UserInterfaceAnimations(TAnimationPreset preset)
        {
            _preset = preset;
        }
    }
}