// IngredientDrawerUIE

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace BigWorld
{
    [CustomPropertyDrawer(typeof(DoubleVector3))]
    public class IngredientDrawerUIE : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Create property container element.
            var container = new VisualElement();

            // Create property fields.
            var xField = new PropertyField(property.FindPropertyRelative("X"));
            var yField = new PropertyField(property.FindPropertyRelative("Y"));
            var zField = new PropertyField(property.FindPropertyRelative("Z"));

            // Add fields to the container.
            container.Add(xField);
            container.Add(yField);
            container.Add(zField);

            return container;
        }
    }
}