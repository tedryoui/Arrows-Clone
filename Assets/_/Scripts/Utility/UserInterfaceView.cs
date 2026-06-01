using UnityEngine;
using UnityEngine.UIElements;

namespace _.Scripts.Utility
{
    public abstract class UserInterfaceView
    {
        private UIDocument _document;

        public UIDocument Document => _document;

        public UserInterfaceView(UIDocument document)
        {
            _document = document;
        }
    }
}