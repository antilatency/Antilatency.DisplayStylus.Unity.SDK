using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Antilatency.Alt.Tracking;
using Antilatency.DeviceNetwork;
using Antilatency.HardwareExtensionInterface;
using Antilatency.HardwareExtensionInterface.Interop;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Antilatency.DisplayStylus.SDK {
    [RequireComponent(typeof(Display))]
    public class StylusesCreator : LifeTimeControllerStateMachine {

        [SerializeField] private GameObject stylusGoTemplate;
        [SerializeField, Header("Default: Stylus\nYou can add your own tags to the list.")] private List<string> _requiredTags = new() { "Stylus" };
        
        public static StylusesCreator Instance => _instance;
        public static event Action<Stylus> OnCreatedStylus;
        public IReadOnlyList<Stylus> Styluses => _styluses;
        
        private Antilatency.HardwareExtensionInterface.ILibrary _hardwareExtensionLibrary;
        private Antilatency.HardwareExtensionInterface.ICotaskConstructor _hardwareExtensionCotaskConstructor;

        private Antilatency.Alt.Tracking.ILibrary _altLibrary;
        private Antilatency.Alt.Tracking.ITrackingCotaskConstructor _altCotaskConstructor;

        private static StylusesCreator _instance;
        private static int _counterStyluses = -1;
        
        private const string _hardwareStylusName = "AntilatencyStylusAlpha";
        private uint _lastCheckedUpdateId;
        private List<Stylus> _styluses = new(2);

#if UNITY_EDITOR

        private void Reset(){
            ValidateStylusTemplate(false);
        }

        private void OnValidate() {
            ValidateStylusTemplate(true);
        }

        public void ValidateStylusTemplate(bool showWarning = true) {
            if (stylusGoTemplate == null) {

                if (showWarning) {
                    Debug.LogWarning("Stylus template can not be null!");
                }

                string[] rootFolderVariants = new[]{
                    "Assets",
                    "Packages"
                };

                foreach (var rootFolder in rootFolderVariants){
                    stylusGoTemplate = AssetDatabase.LoadAssetAtPath<GameObject>($"{rootFolder}/com.Antilatency.Displaystylus.Unity.Sdk/StylusTemplate.prefab");
                    if (stylusGoTemplate != null){
                        break;
                    }
                }

                if (stylusGoTemplate.GetComponent<Stylus>() == null) {
                    Debug.LogError("Default stylus template don't contain Stylus.cs.");
                }
            }
        }
#endif

        protected override void Create() {
            _hardwareExtensionLibrary = Antilatency.HardwareExtensionInterface.Library.load();
            _hardwareExtensionCotaskConstructor = _hardwareExtensionLibrary.getCotaskConstructor();

            _altLibrary = Antilatency.Alt.Tracking.Library.load();
            _altCotaskConstructor = _altLibrary.createTrackingCotaskConstructor();
            
            base.Create();

            if (_instance != null) {
                Debug.LogError("StylusesCreator has dublicates. Will remove this new instance!");
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        protected override void Destroy() {
            base.Destroy();
            _instance = null;
            
            Antilatency.Utils.SafeDispose(ref _altCotaskConstructor);
            Antilatency.Utils.SafeDispose(ref _altLibrary);
            Antilatency.Utils.SafeDispose(ref _hardwareExtensionCotaskConstructor);
            Antilatency.Utils.SafeDispose(ref _hardwareExtensionLibrary);
        }

        protected override IEnumerable StateMachine() {
            string status = string.Empty;
            
            WaitForNetworks:
            if (Destroying) { yield break; }

            var deviceNetworkProvider = GetComponentInParent<Antilatency.SDK.DeviceNetwork>();

            status = "Finding Network Provider";
            if (deviceNetworkProvider == null) {
                yield return status;
                goto WaitForNetworks;
            }

            var network = deviceNetworkProvider.NativeNetwork;
            
            if (network.IsNull()) {
                yield return status;
                goto WaitForNetworks;
            }
            
            FindingDisplay:
            if (Destroying) { yield break; }
            status = "Finding Display";
            
            Display display = GetComponentInParent<Display>();

            if (display == null){
                yield return status;
                goto FindingDisplay;
            }
            
            WaitingEnvironment:
            if (Destroying) { yield break; }
            status = "Waiting Environment";

            var environment = display.GetEnvironment();
            if (environment.IsNull()) {
                yield return status;
                goto WaitingEnvironment;
            }

            status = null;
            
            Working:
            if(Destroying) { yield break;}
            var isCaught = false;

            uint updateId = deviceNetworkProvider.NativeNetwork.getUpdateId();
            if (_lastCheckedUpdateId != updateId) {
                _lastCheckedUpdateId = updateId;

                if (display.GetEnvironment().IsNull()) {
                    yield return status;
                    goto WaitForNetworks;
                }

                NodeHandle[] extensionSupportedNodes;
                NodeHandle[] allAltTrackingNodes = Array.Empty<NodeHandle>();
                NodeHandle extensionNode;

                try {
                    extensionSupportedNodes = _hardwareExtensionCotaskConstructor.findSupportedNodes(network);
                    allAltTrackingNodes = _altCotaskConstructor.findSupportedNodes(network);

                    //Finding builded by antilatency stylus.
                    extensionNode = extensionSupportedNodes.FirstOrDefault(n =>
                        network.nodeGetStringProperty(n, Antilatency.DeviceNetwork.Interop.Constants.HardwareNameKey).Contains(_hardwareStylusName) &&
                        network.nodeGetStatus(n) == NodeStatus.Idle);

                    //Try find crafted by user stylus.
                    if (extensionNode == NodeHandle.Null) {
                        foreach (string customStylusTag in _requiredTags) {
                            if (customStylusTag != string.Empty) {
                                extensionNode = extensionSupportedNodes.FirstOrDefault(n =>
                                    network.nodeGetStringProperty(n, "Tag").Equals(customStylusTag) &&
                                    network.nodeGetStatus(n) == NodeStatus.Idle);
                            } else {

                                if (Application.isEditor || Debug.isDebugBuild) {
                                    Debug.LogError("Stylus tag == string.Empty. Fix the list of tags for the styluses.");
                                }
                            }
                        }
                    }
                }
                catch {
                    extensionNode = NodeHandle.Null;
                }

                if (extensionNode == NodeHandle.Null) {
                    yield return status;
                    goto Working;
                }

                NodeHandle altNode = allAltTrackingNodes.FirstOrDefault(n => network.nodeGetParent(n) == extensionNode);

                if (altNode == NodeHandle.Null) {
                    yield return status;
                    goto Working;
                }

                ITrackingCotask trackingCotask = null;
                Antilatency.HardwareExtensionInterface.ICotask extensionsCotask = null;
                IInputPin inputPin = null;
                
                try {
                    extensionsCotask = _hardwareExtensionCotaskConstructor.startTask(network, extensionNode);
                    inputPin = extensionsCotask.createInputPin(Pins.IO1);
                    extensionsCotask.run();
                }
                catch (Exception e) {

                    if (Application.isEditor || Debug.isDebugBuild) {
                        Debug.LogError($"Stylus start extension task failed. {e.Message} \n {e.StackTrace}");
                    }

                    Antilatency.Utils.SafeDispose(ref inputPin);
                    Antilatency.Utils.SafeDispose(ref extensionsCotask);
                    isCaught = true;
                }

                if (isCaught) {
                    yield return status;
                    goto Working;
                }

                try {
                    trackingCotask = _altCotaskConstructor.startTask(network, altNode, display.GetEnvironment());

                    if (trackingCotask.IsNull()) {
                        throw new Exception();
                    }
                }
                catch (Exception e) {

                    if (Application.isEditor || Debug.isDebugBuild) {
                        Debug.LogError($"Stylus start tracking task failed. {e.Message} \n {e.StackTrace}");
                    }

                    Antilatency.Utils.SafeDispose(ref inputPin);
                    Antilatency.Utils.SafeDispose(ref trackingCotask);
                    Antilatency.Utils.SafeDispose(ref extensionsCotask);
                    isCaught = true;
                }
                
                if (isCaught) {
                    yield return status;
                    goto Working;
                }

                _counterStyluses++;
                int idStylus = _counterStyluses;

                GameObject go = Instantiate(stylusGoTemplate, Vector3.zero, Quaternion.identity, transform);
                Stylus stylus = go.GetComponent<Stylus>();
                stylus.Initialize(idStylus,display, trackingCotask, extensionsCotask, inputPin);
                status = string.Empty;
                stylus.OnDestroying += OnDestroyingStylus;
                OnCreatedStylus?.Invoke(stylus);
                _styluses.Add(stylus);

                if (Application.isEditor || Debug.isDebugBuild) {
                    Debug.Log($"StylusCreator: Stylus created successfully -> {idStylus}");
                    Debug.Log($"Device network update id: {_lastCheckedUpdateId}");
                }
            }

            if (!Destroying) {
                yield return status;
                goto Working;
            }
        }

        private void OnDestroyingStylus(Stylus stylus) {
            if(stylus == null) {
                return;
            }

            stylus.OnDestroying -= OnDestroyingStylus;
            _styluses.Remove(stylus);
        }
    }
}