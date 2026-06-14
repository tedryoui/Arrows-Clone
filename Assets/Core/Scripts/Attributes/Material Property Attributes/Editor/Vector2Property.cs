using UnityEditor;
using UnityEngine;

namespace _.Scripts.Attributes.Material_Property_Attributes.Editor
{
    public class Vector2Property : MaterialPropertyDrawer
    {
        // Overrides the default layout in the Inspector
        public override void OnGUI(Rect position, MaterialProperty rectProperty, string label, MaterialEditor editor)
        {
            // Read the current 4D vector value from the material
            Vector4 val = rectProperty.vectorValue;

            // Convert it to a 2D vector for the UI field
            Vector2 vec2 = new Vector2(val.x, val.y);
            
            EditorGUI.BeginChangeCheck();
        
            // Draw the 2D vector field in the inspector
            vec2 = EditorGUI.Vector2Field(position, label, vec2);

            if (EditorGUI.EndChangeCheck())
            {
                // Write the modified 2D data back into the 4D vector slot (Z and W stay 0)
                rectProperty.vectorValue = new Vector4(vec2.x, vec2.y, 0, 0);
            }
        }

        // Prevents the inspector layout from overlapping with other properties
        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}