using System;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {

    [Serializable]
    public class DisplayProperties {
        public string HardwareName { get; private set; }
        public Vector3 ScreenPosition;
        public Vector3 ScreenX;
        public Vector3 ScreenY;

        public DisplayProperties(){
            ScreenPosition = Vector3.zero;
            ScreenX = new Vector3(0.1505f, 0, 0);
            ScreenY = new Vector3(0f, 0.095f, 0);
        }
        
        public DisplayProperties(Antilatency.DeviceNetwork.INetwork network, Antilatency.DeviceNetwork.NodeHandle node) {
          
            using (var propertiesReader = new AdnPropertiesReader(network, node)) {

                HardwareName = network.nodeGetStringProperty(node, Antilatency.DeviceNetwork.Interop.Constants.HardwareNameKey);
                ScreenPosition = propertiesReader.TryRead("sys/ScreenPosition", AdnPropertiesReader.ReadVector3).Value;
                ScreenX = propertiesReader.TryRead("sys/ScreenAxisX", AdnPropertiesReader.ReadVector3).Value;
                ScreenY = propertiesReader.TryRead("sys/ScreenAxisY", AdnPropertiesReader.ReadVector3).Value;
            }
        }
        
    }
}