using UnityEditor;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK{
    public static class EditorMenu{
        
        [MenuItem("Display Stylus/Create In Scene")]
        public static void CreateInScene(){

            GameObject displayHandle = new GameObject("DisplayHandle");
            GameObject display = new GameObject("Display");
            
            display.transform.SetParent(displayHandle.transform);
            display.transform.localPosition = Vector3.zero;
            display.transform.localRotation = Quaternion.identity;

            displayHandle.AddComponent<DisplayHandle>();

            display.AddComponent<Antilatency.SDK.DeviceNetwork>();
            display.AddComponent<Display>();
            display.AddComponent<StylusesCreator>();
        }
    }
}
