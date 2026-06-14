using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _.Scripts.Editor.UI_Toolkit
{
    public class UIToolkitModal : EditorWindow
    {
        private string         _defaultName;
        private TextField      _fileNameTextField;
        private Action<string> _onConfirm;

        public void CreateGUI()
        {
            var root = rootVisualElement;

            root.style.paddingTop    = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft   = 10;
            root.style.paddingRight  = 10;

            _fileNameTextField       = new TextField("File Name");
            _fileNameTextField.value = _defaultName;
            root.Add(_fileNameTextField);

            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection  = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.FlexEnd;
            buttonContainer.style.marginTop      = 15;

            var cancelBtn = new Button(() => Close()) { text = "Cancel" };
            cancelBtn.style.marginRight = 5;

            var confirmBtn = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_fileNameTextField.value))
                {
                    _onConfirm?.Invoke(_fileNameTextField.value);
                    Close();
                }
            }) { text = "Confirm" };

            buttonContainer.Add(cancelBtn);
            buttonContainer.Add(confirmBtn);
            root.Add(buttonContainer);
        }

        public static void Open(string defaultName, Action<string> onConfirm)
        {
            var window = GetWindow<UIToolkitModal>("Save File");
            window._defaultName = defaultName;
            window._onConfirm   = onConfirm;

            window.minSize = new Vector2(300, 120);
            window.maxSize = new Vector2(300, 120);
            window.ShowModalUtility();
        }
    }
}