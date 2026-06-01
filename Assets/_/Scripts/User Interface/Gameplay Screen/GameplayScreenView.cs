using _.Scripts.Utility;
using UnityEngine.UIElements;

namespace _.Scripts.User_Interface.Gameplay_Screen
{
    public class GameplayScreenView : UserInterfaceView
    {
        private ProgressBar _progressBar;

        public ProgressBar ProgressBar => _progressBar;

        public GameplayScreenView(UIDocument document) : base(document)
        {
            _progressBar = document.rootVisualElement.Q<ProgressBar>("Timer");
        }
    }
}