using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CameraSelectionAttribute))]
public class CameraSelectionDrawer : PropertyDrawer
{
    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        var devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            EditorGUI.PropertyField(
                position,
                property,
                label
            );

            return;
        }

        var names = new string[devices.Length];

        for (var i = 0; i < devices.Length; i++)
        {
            names[i] = $"{i}: {devices[i].name}";
        }

        property.intValue = Mathf.Clamp(
            property.intValue,
            0,
            devices.Length - 1
        );

        property.intValue = EditorGUI.Popup(
            position,
            label.text,
            property.intValue,
            names
        );
    }
}
