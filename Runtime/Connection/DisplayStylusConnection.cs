using System;
using Antilatency.Alt.Environment;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    public enum DisplayStylusConnectionMode {
        LocalAdn,
        Proxy
    }

    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class DisplayStylusConnection : LifeTimeController {
        [SerializeField] private DisplayStylusConnectionMode mode = DisplayStylusConnectionMode.LocalAdn;
        [SerializeField] private Antilatency.SDK.DeviceNetwork localDeviceNetwork;
        [SerializeField] private string proxyBaseUrl = "http://127.0.0.1:48192";
        [SerializeField, Min(0.001f)] private float extrapolationSeconds = 0.042f;
        [SerializeField, Min(0.1f)] private float proxyReconnectDelaySeconds = 1.0f;
        [SerializeField] private string hardwareNameContains = "AntilatencyStylusAlpha";
        [SerializeField] private string[] stylusTags = { "Stylus" };
        [SerializeField] private bool manageLocalDeviceNetworkActivation = true;

        public event Action<DisplayStylusFrame> FrameUpdated;

        public DisplayStylusConnectionMode Mode {
            get => mode;
            set {
                if (mode == value) {
                    return;
                }
                mode = value;
                if (isActiveAndEnabled) {
                    RestartSource();
                }
            }
        }

        public Antilatency.SDK.DeviceNetwork LocalDeviceNetwork {
            get => localDeviceNetwork;
            set => localDeviceNetwork = value;
        }

        public string ProxyBaseUrl {
            get => proxyBaseUrl;
            set => proxyBaseUrl = value;
        }

        public float ExtrapolationSeconds {
            get => extrapolationSeconds;
            set => extrapolationSeconds = Mathf.Max(0.001f, value);
        }

        public DisplayStylusFrame LatestFrame { get; private set; }
        public string ConnectionStatus => _source?.Status ?? _initializationError ?? "Not initialized";
        public bool IsReady => LatestFrame?.Display?.Connected == true;
        public IEnvironment LocalEnvironment => _source?.LocalEnvironment;

        /// <summary>
        /// Creates a lease-aware control client for proxy write commands. The returned writer is
        /// independent of snapshot streaming and must be disposed by the caller.
        /// </summary>
        public DisplayStylusProxyWriter CreateProxyWriter(string clientId) {
            if (mode != DisplayStylusConnectionMode.Proxy) {
                throw new InvalidOperationException("Proxy writes are available only while Connection Mode is Proxy.");
            }
            return new DisplayStylusProxyWriter(proxyBaseUrl, clientId);
        }

        /// <summary>Recreates the active data source after changing its settings at runtime.</summary>
        public void Reconnect() {
            if (isActiveAndEnabled) {
                RestartSource();
            }
        }

        private IDisplayStylusDataSource _source;
        private DisplayStylusConnectionMode _activeMode;
        private string _initializationError;

        private void Awake() {
            if (mode == DisplayStylusConnectionMode.Proxy) {
                ReleaseManagedLocalDeviceNetwork();
            }
        }

        protected override void Create() {
            base.Create();
            RestartSource();
            Application.onBeforeRender += OnBeforeRender;
        }

        private void Update() {
            if (_activeMode != mode) {
                RestartSource();
            }
            SampleSource();
        }

        [BeforeRenderOrder(-30000)]
        private void OnBeforeRender() {
            SampleSource();
        }

        protected override void Destroy() {
            Application.onBeforeRender -= OnBeforeRender;
            DisposeSource();
            LatestFrame = null;
            base.Destroy();
        }

        private void RestartSource() {
            DisposeSource();
            LatestFrame = null;
            _initializationError = null;
            _activeMode = mode;

            try {
                if (localDeviceNetwork == null) {
                    localDeviceNetwork = GetComponent<Antilatency.SDK.DeviceNetwork>();
                }

                if (mode == DisplayStylusConnectionMode.LocalAdn) {
                    if (localDeviceNetwork == null) {
                        localDeviceNetwork = gameObject.AddComponent<Antilatency.SDK.DeviceNetwork>();
                    }
                    if (manageLocalDeviceNetworkActivation) {
                        localDeviceNetwork.enabled = true;
                    }
                    _source = new LocalAdnDisplayStylusDataSource(
                        localDeviceNetwork,
                        hardwareNameContains,
                        stylusTags ?? Array.Empty<string>());
                }
                else {
                    ReleaseManagedLocalDeviceNetwork();
                    _source = new ProxyDisplayStylusDataSource(proxyBaseUrl, proxyReconnectDelaySeconds);
                }
            }
            catch (Exception exception) {
                Debug.LogException(exception, this);
                _initializationError = $"Connection initialization failed: {exception.Message}";
            }
        }

        private void SampleSource() {
            if (_source == null) {
                return;
            }

            _source.Tick(extrapolationSeconds);
            if (_source.TryGetLatestFrame(out var frame) &&
                (LatestFrame == null || frame.Sequence != LatestFrame.Sequence)) {
                LatestFrame = frame;
                FrameUpdated?.Invoke(frame);
            }
        }

        private void DisposeSource() {
            _source?.Dispose();
            _source = null;
        }

        private void ReleaseManagedLocalDeviceNetwork() {
            if (!manageLocalDeviceNetworkActivation || localDeviceNetwork == null) {
                return;
            }

            var network = localDeviceNetwork;
            localDeviceNetwork = null;
            network.enabled = false;
            Destroy(network);
        }

#if UNITY_EDITOR
        private void Reset() {
            localDeviceNetwork = GetComponent<Antilatency.SDK.DeviceNetwork>();
        }

        private void OnValidate() {
            extrapolationSeconds = Mathf.Max(0.001f, extrapolationSeconds);
            proxyReconnectDelaySeconds = Mathf.Max(0.1f, proxyReconnectDelaySeconds);
            if (string.IsNullOrWhiteSpace(proxyBaseUrl)) {
                proxyBaseUrl = "http://127.0.0.1:48192";
            }

            if (!Application.isPlaying &&
                mode == DisplayStylusConnectionMode.Proxy &&
                manageLocalDeviceNetworkActivation &&
                localDeviceNetwork != null) {
                var network = localDeviceNetwork;
                UnityEditor.EditorApplication.delayCall += () => {
                    if (this == null || network == null ||
                        mode != DisplayStylusConnectionMode.Proxy ||
                        !manageLocalDeviceNetworkActivation) {
                        return;
                    }
                    if (localDeviceNetwork == network) {
                        localDeviceNetwork = null;
                    }
                    UnityEditor.Undo.DestroyObjectImmediate(network);
                    UnityEditor.EditorUtility.SetDirty(this);
                };
            }
        }
#endif
    }
}
