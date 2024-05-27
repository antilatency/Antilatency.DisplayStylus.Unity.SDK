using System.Collections;
using System.Linq;
using Antilatency.Alt.Environment;
using Antilatency.DeviceNetwork;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK{
    [RequireComponent(typeof(Antilatency.SDK.DeviceNetwork))]
    public class Display : LifeTimeControllerStateMachine, IEnvironmentProvider{

        public bool SyncWithPhysicalDisplayRotation = true;
        public DisplayProperties DisplayProperties = new DisplayProperties();
        
        public IEnvironment Environment => GetEnvironment();
        private const string DeviceHardwareName = "AntilatencyPhysicalConfigurableEnvironment";
        private IEnvironment _environment;
        private Antilatency.PhysicalConfigurableEnvironment.ILibrary _physicalConfigurableEnvironmentLibrary;
        private Antilatency.Alt.Environment.Selector.ILibrary _environmentSelectorLibrary;
        private Antilatency.PhysicalConfigurableEnvironment.ICotaskConstructor _cotaskContructor;
        private Antilatency.PhysicalConfigurableEnvironment.ICotask _cotask;

        protected override IEnumerable StateMachine(){

            string status;

            _physicalConfigurableEnvironmentLibrary = Antilatency.PhysicalConfigurableEnvironment.Library.load();
            _cotaskContructor = _physicalConfigurableEnvironmentLibrary.createCotaskConstructor();
            _environmentSelectorLibrary = Antilatency.Alt.Environment.Selector.Library.load();

            WaitingForNetwork:
            if (Destroying) yield break;
            status = "Waiting for DeviceNetwork";

            INetwork network = GetComponent<Antilatency.SDK.DeviceNetwork>()?.NativeNetwork;

            if (network.IsNull()){
                yield return status;
                goto WaitingForNetwork;
            }

            ConnectingToDevice:
            if (Destroying) yield break;
            status = "ConnectingToDevice";
            NodeHandle deviceNode = FindSupportingDeviceNode(network);

            if (deviceNode == NodeHandle.Null){
                yield return status;
                goto ConnectingToDevice;
            }

            DisplayProperties = new DisplayProperties(network, deviceNode);
            
            using (_cotask = _cotaskContructor.startTask(network, deviceNode)){
                while (!_cotask.IsNull() && !_cotask.isTaskFinished()){

                    if (_environment == null){
                        status = "Fetching environment";
                        yield return status;

                        var configId = _cotask.getConfigId();
                        string environmentCode = _cotask.getEnvironment(configId);
                        _environment = _environmentSelectorLibrary.createEnvironment(environmentCode);
                    }

                    yield return null;
                    if (Destroying) yield break;
                }

                goto ConnectingToDevice;
            }
        }

        protected override void Destroy(){
            base.Destroy();

            Utils.SafeDispose(ref _cotask);
            Utils.SafeDispose(ref _cotaskContructor);
            Utils.SafeDispose(ref _environmentSelectorLibrary);
            Utils.SafeDispose(ref _physicalConfigurableEnvironmentLibrary);
        }

        private NodeHandle FindSupportingDeviceNode(INetwork network){
            var nodes = network.getNodes().Where(i => network.nodeGetStatus(i) == NodeStatus.Idle);

            foreach (var node in nodes){
                if (network.nodeGetStringProperty(node, DeviceNetwork.Interop.Constants.HardwareNameKey).Equals(DeviceHardwareName)){
                    return node;
                }
            }

            return NodeHandle.Null;
        }

        public IEnvironment GetEnvironment(){
            return _environment;
        }
        
        public Vector2 GetHalfScreenSize() {
            return new Vector2(DisplayProperties.ScreenAxisX.magnitude, DisplayProperties.ScreenAxisY.magnitude);
        }
        
        public Matrix4x4 GetScreenToEnvironment() {
            var x = DisplayProperties.ScreenAxisX.normalized;
            var y = DisplayProperties.ScreenAxisY.normalized;
            Vector4 w = DisplayProperties.ScreenPosition;
            w.w = 1;
            return new Matrix4x4(x, y, Vector3.Cross(x, y), w);
        }
    }
}