using BigWorld.Doubles;
using UnityEditor;
using UnityEngine;

namespace BigWorld
{

[CustomPropertyDrawer(typeof(DoubleVector3))]
public class DoubleVector3Drawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Start property drawing
        EditorGUI.BeginProperty(position, label, property);

        // Draw fields for x, y, z
        SerializedProperty xProp = property.FindPropertyRelative("X");
        SerializedProperty yProp = property.FindPropertyRelative("Y");
        SerializedProperty zProp = property.FindPropertyRelative("Z");

        EditorGUILayout.BeginHorizontal();
        var newVector = EditorGUILayout.Vector3Field(property.displayName, new Vector3((float)xProp.doubleValue, (float)yProp.doubleValue, (float)zProp.doubleValue));
        xProp.doubleValue = newVector.x;
        yProp.doubleValue = newVector.y;
        zProp.doubleValue = newVector.z;
        EditorGUILayout.EndHorizontal();

        // End property drawing
        EditorGUI.EndProperty();
    }
}
    
}