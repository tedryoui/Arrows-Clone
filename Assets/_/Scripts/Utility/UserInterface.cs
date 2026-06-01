namespace _.Scripts.Utility
{
    public abstract class UserInterface
    {
        protected object _view;
        protected object _animations;

        protected UserInterface(object view, object animations)
        {
            _view       = view;
            _animations = animations;
        }

        public abstract void Show(bool animate);

        public abstract void Hide(bool animate);
    }
    
    public abstract class UserInterface<T1, T2> : UserInterface
    where T1 : UserInterfaceView
    where T2 : UserInterfaceAnimations
    {
        private T1 _cachedView;
        private T2 _cachedAnimations;

        public T1 View       => _cachedView ??= _view as T1;
        public T2 Animations => _cachedAnimations ??= _animations as T2;

        protected UserInterface(T1 view, T2 animations) : base(view, animations)
        {
            
        }

        protected abstract void Bind();
    }
}