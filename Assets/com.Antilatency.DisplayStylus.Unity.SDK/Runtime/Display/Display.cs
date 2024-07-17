using System;
using System.Collections;
using System.Linq;
using Antilatency.Alt.Environment;
using Antilatency.DeviceNetwork;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK{
    [RequireComponent(typeof(Antilatency.SDK.DeviceNetwork))]
    public class Display : LifeTimeControllerStateMachine, IEnvironmentProvider{

        public bool SyncWithPhysicalDisplayRotation = false;
        
        public Vector3 ScreenPosition = Vector3.zero;
        public Vector3 ScreenX = new(0.1505f, 0, 0);
        public Vector3 ScreenY = new(0f, 0.095f, 0);
        
        public Quaternion EnvironmentRotation{
            get{
                var rotationEnvironment = _environment?.QueryInterface<IOrientationAwareEnvironment>();

                if (rotationEnvironment != null) {
                    var q = rotationEnvironment.getRotation();
                    
                    // TODO: remove isNormalized check after library update
                    var isNormalized = System.Math.Abs(
                        ((double)q.x * q.x + (double)q.y * q.y + (double)q.z * q.z + (double)q.w * q.w) - 1.0f) < 1e-6;

                    if (!isNormalized && (Debug.isDebugBuild || Application.isEditor)) {
                        Debug.LogWarning($"Quaternion is not normalized. x:{q.x} y:{q.y} z:{q.z} w:{q.w}");
                    }

                    return !isNormalized ? Quaternion.identity : q;
                }

                return Quaternion.identity;
            }
        }
        
        private IEnvironment _environment;
        private Antilatency.PhysicalConfigurableEnvironment.ILibrary _physicalConfigurableEnvironmentLibrary;
        private Antilatency.Alt.Environment.Selector.ILibrary _environmentSelectorLibrary;
        private Antilatency.PhysicalConfigurableEnvironment.ICotaskConstructor _cotaskConstructor;
        private Antilatency.PhysicalConfigurableEnvironment.ICotask _cotask;
        
        private const string TheEnvironmentWasNotCreatedMessage = "The environment was not created";
        
        protected override IEnumerable StateMachine(){

            string status;

            _physicalConfigurableEnvironmentLibrary = PhysicalConfigurableEnvironment.Library.load();
            _cotaskConstructor = _physicalConfigurableEnvironmentLibrary.createCotaskConstructor();
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
            NodeHandle deviceNode = _cotaskConstructor.findSupportedNodes(network).FirstOrDefault();

            if (deviceNode == NodeHandle.Null){
                var lastUpdateId = network.getUpdateId();
                while (lastUpdateId == network.getUpdateId()){
                    if (Destroying) yield break;
                    yield return status;
                }

                goto ConnectingToDevice;
            }
                
            using (_cotask = _cotaskConstructor.startTask(network, deviceNode)){

                Func<bool> isHealthyCotask = () => !_cotask.IsNull() && !_cotask.isTaskFinished();
            
                if (isHealthyCotask.Invoke()){
                    
                    //Reading properties and creating an environment
                    try{
                        ScreenPosition = _cotask.getScreenPosition();
                        ScreenX = _cotask.getScreenX();
                        ScreenY = _cotask.getScreenY();

                        var configId = _cotask.getConfigId();
                        string environmentCode = _cotask.getEnvironment(configId);
                        _environment = _environmentSelectorLibrary.createEnvironment(environmentCode);
                    }
                    catch (Exception e){
                        _environment = null;
                        Debug.LogException(e);
                    }
                }

                status = _environment != null ? null : TheEnvironmentWasNotCreatedMessage;
                
                while (isHealthyCotask.Invoke()){
                    if (Destroying) yield break;
                    yield return status;
                }
            }

            goto ConnectingToDevice;
        }
        
        protected override void Destroy(){
            base.Destroy();

            Utils.SafeDispose(ref _cotask);
            Utils.SafeDispose(ref _cotaskConstructor);
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