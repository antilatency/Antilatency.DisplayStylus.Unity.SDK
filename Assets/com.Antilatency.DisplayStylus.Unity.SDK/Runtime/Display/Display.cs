using System.Collections;
using System.Linq;
using Antilatency.Alt.Environment;
using Antilatency.DeviceNetwork;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK{
    [RequireComponent(typeof(Antilatency.SDK.DeviceNetwork))]
    public class Display : LifeTimeControllerStateMachine, IEnvironmentProvider{

        public bool SyncWithPhysicalDisplayRotation = true;
        
        public Vector3 ScreenPosition = Vector3.zero;
        public Vector3 ScreenX = new(0.1505f, 0, 0);
        public Vector3 ScreenY = new(0f, 0.095f, 0);
        
        public Quaternion EnvironmentRotation{
            get{
                var rotationEnvironment = _environment?.QueryInterface<IOrientationAwareEnvironment>();

                if (rotationEnvironment != null){
                    return rotationEnvironment.getRotation().normalized;
                }

                return Quaternion.identity;
            }
        }
        
        private IEnvironment _environment;
        private Antilatency.PhysicalConfigurableEnvironment.ILibrary _physicalConfigurableEnvironmentLibrary;
        private Antilatency.Alt.Environment.Selector.ILibrary _environmentSelectorLibrary;
        private Antilatency.PhysicalConfigurableEnvironment.ICotaskConstructor _cotaskContructor;
        private Antilatency.PhysicalConfigurableEnvironment.ICotask _cotask;

        protected override IEnumerable StateMachine(){

            string status;

            _physicalConfigurableEnvironmentLibrary = PhysicalConfigurableEnvironment.Library.load();
            _cotaskContructor = _physicalConfigurableEnvironmentLibrary.createCotaskConstructor();
            _environmentSelectorLibrary = Alt.Environment.Selector.Library.load();

            WaitingForNetwork:
            if (Destroying) yield break;
            status = "Waiting For DeviceNetwork";

            INetwork network = GetComponent<Antilatency.SDK.DeviceNetwork>()?.NativeNetwork;

            if (network.IsNull()){
                yield return status;
                goto WaitingForNetwork;
            }

            ConnectingToDevice:
            if (Destroying) yield break;
            status = "Connecting To Device";
            NodeHandle deviceNode = _cotaskContructor.findSupportedNodes(network).FirstOrDefault();

            if (deviceNode == NodeHandle.Null){
                var lastUpdateId = network.getUpdateId();
                while (lastUpdateId == network.getUpdateId()){
                    if (Destroying) yield break;
                    yield return status;
                }

                goto ConnectingToDevice;
            }
            
            using (_cotask = _cotaskContructor.startTask(network, deviceNode)){
                while (!_cotask.IsNull() && !_cotask.isTaskFinished()){

                    if (Destroying) yield break;
                    
                    if (_environment == null){
                        try{
                            ScreenPosition = _cotask.getScreenPosition();
                            ScreenX = _cotask.getScreenX();
                            ScreenY = _cotask.getScreenY();
                                
                            var configId = _cotask.getConfigId();
                            string environmentCode = _cotask.getEnvironment(configId);
                            _environment = _environmentSelectorLibrary.createEnvironment(environmentCode);
                        }
                        catch{
                            Debug.LogError("Error while reading display properties.");
                            throw;
                        }
                    }

                    yield return null;
                }
            }

            goto ConnectingToDevice;
        }
        
        protected override void Destroy(){
            base.Destroy();

            Utils.SafeDispose(ref _cotask);
            Utils.SafeDispose(ref _cotaskContructor);
            Utils.SafeDispose(ref _environmentSelectorLibrary);
            Utils.SafeDispose(ref _physicalConfigurableEnvironmentLibrary);
        }
        
        public Vector2 GetHalfScreenSize() {
            return new Vector2(ScreenX.magnitude, ScreenY.magnitude);
        }
        
        public Matrix4x4 GetScreenToEnvironment() {
            var x = ScreenX.normalized;
            var y = ScreenY.normalized;
            Vector4 w = ScreenPosition;
            w.w = 1;
            return new Matrix4x4(x, y, Vector3.Cross(x, y), w);
        }

        public IEnvironment GetEnvironment(){
            return _environment;
        }
    }
}