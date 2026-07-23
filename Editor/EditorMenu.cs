using UnityEditor;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    public static class EditorMenu {
        [MenuItem("Display Stylus/Create In Scene")]
        public static void CreateInScene() {
            var displayHandle = new GameObject("DisplayHandle");
            var display = new GameObject("Display");
            Undo.RegisterCreatedObjectUndo(displayHandle, "Create Display Stylus");

            display.transform.SetParent(displayHandle.transform);
            display.transform.localPosition = Vector3.zero;
            display.transform.localRotation = Quaternion.identity;

            displayHandle.AddComponent<DisplayHandle>();
            display.AddComponent<DisplayStylusConnection>();
            display.AddComponent<Display>();
            display.AddComponent<StylusesCreator>();

            Selection.activeGameObject = displayHandle;
        }
    }
}
