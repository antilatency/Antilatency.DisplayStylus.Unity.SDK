using System;
using System.Collections.Generic;
using System.Linq;
using Antilatency.Alt.Environment;
using Antilatency.Alt.Tracking;
using Antilatency.DeviceNetwork;
using Antilatency.HardwareExtensionInterface;
using Antilatency.HardwareExtensionInterface.Interop;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    internal sealed class LocalAdnDisplayStylusDataSource : IDisplayStylusDataSource {
        private readonly Antilatency.SDK.DeviceNetwork _provider;
        private readonly string _hardwareNameContains;
        private readonly string[] _stylusTags;
        private readonly Dictionary<uint, LocalStylus> _styluses = new();

        private Antilatency.PhysicalConfigurableEnvironment.ILibrary _physicalEnvironmentLibrary;
        private Antilatency.PhysicalConfigurableEnvironment.ICotaskConstructor _physicalEnvironmentConstructor;
        private Antilatency.Alt.Environment.Selector.ILibrary _environmentSelectorLibrary;
        private Antilatency.Alt.Tracking.ILibrary _trackingLibrary;
        private ITrackingCotaskConstructor _trackingConstructor;
        private Antilatency.HardwareExtensionInterface.ILibrary _hardwareExtensionLibrary;
        private Antilatency.HardwareExtensionInterface.ICotaskConstructor _hardwareExtensionConstructor;

        private INetwork _network;
        private Antilatency.PhysicalConfigurableEnvironment.ICotask _displayCotask;
        private IEnvironment _environment;
        private uint _lastUpdateId = uint.MaxValue;
        private long _sequence;
        private DisplayStylusFrame _latestFrame;

        public LocalAdnDisplayStylusDataSource(
            Antilatency.SDK.DeviceNetwork provider,
            string hardwareNameContains,
            string[] stylusTags) {
            _provider = provider;
            _hardwareNameContains = hardwareNameContains ?? string.Empty;
            _stylusTags = stylusTags ?? Array.Empty<string>();

            try {
                _physicalEnvironmentLibrary = Antilatency.PhysicalConfigurableEnvironment.Library.load();
                _physicalEnvironmentConstructor = _physicalEnvironmentLibrary.createCotaskConstructor();
                _environmentSelectorLibrary = Antilatency.Alt.Environment.Selector.Library.load();
                _trackingLibrary = Antilatency.Alt.Tracking.Library.load();
                _trackingConstructor = _trackingLibrary.createTrackingCotaskConstructor();
                _hardwareExtensionLibrary = Antilatency.HardwareExtensionInterface.Library.load();
                _hardwareExtensionConstructor = _hardwareExtensionLibrary.getCotaskConstructor();
                Status = "Waiting for local ADN";
            }
            catch (Exception exception) {
                Status = $"Failed to initialize local ADN: {exception.Message}";
                DisposeLibraries();
            }
        }

        public string Status { get; private set; }
        public IEnvironment LocalEnvironment => _environment;

        public void Tick(float extrapolationSeconds) {
            if (_provider == null || _physicalEnvironmentConstructor.IsNull()) {
                Status = _provider == null
                    ? "Local ADN mode requires an Antilatency SDK DeviceNetwork component"
                    : Status;
                PublishDisconnectedFrame();
                return;
            }

            var network = _provider.NativeNetwork;
            if (network.IsNull()) {
                ResetDeviceTasks();
                Status = "Waiting for local ADN";
                PublishDisconnectedFrame();
                return;
            }

            if (!ReferenceEquals(_network, network)) {
                ResetDeviceTasks();
                _network = network;
                _lastUpdateId = uint.MaxValue;
            }

            try {
                RefreshTasks(network);
                _latestFrame = SampleFrame(Mathf.Max(0.001f, extrapolationSeconds));
                Status = _latestFrame.Display?.Connected == true
                    ? "Local ADN connected"
                    : "Waiting for a physical display";
            }
            catch (Exception exception) {
                Status = $"Local ADN error: {exception.Message}";
                ResetDeviceTasks();
                PublishDisconnectedFrame();
            }
        }

        public bool TryGetLatestFrame(out DisplayStylusFrame frame) {
            frame = _latestFrame;
            return frame != null;
        }

        public void Dispose() {
            ResetDeviceTasks();
            _network = null;
            DisposeLibraries();
        }

        private void RefreshTasks(INetwork network) {
            var displayFinished = !_displayCotask.IsNull() && _displayCotask.isTaskFinished();
            var stylusFinished = _styluses.Values.Any(stylus => stylus.IsFinished);
            var updateId = network.getUpdateId();
            if (updateId == _lastUpdateId && !displayFinished && !stylusFinished) {
                return;
            }

            _lastUpdateId = updateId;
            RemoveFinishedTasks();
            if (_displayCotask.IsNull()) {
                TryStartDisplay(network);
            }
            if (!_environment.IsNull()) {
                TryStartStyluses(network);
            }
        }

        private void TryStartDisplay(INetwork network) {
            foreach (var node in _physicalEnvironmentConstructor.findSupportedNodes(network)) {
                if (network.nodeGetStatus(node) != NodeStatus.Idle) {
                    continue;
                }

                try {
                    _displayCotask = _physicalEnvironmentConstructor.startTask(network, node);
                    RecreateEnvironment();
                    return;
                }
                catch (Exception) {
                    Antilatency.Utils.SafeDispose(ref _environment);
                    Antilatency.Utils.SafeDispose(ref _displayCotask);
                }
            }
        }

        private void RecreateEnvironment() {
            var environmentCode = _displayCotask.getEnvironment(_displayCotask.getConfigId());
            var replacement = _environmentSelectorLibrary.createEnvironment(environmentCode);
            if (replacement.IsNull()) {
                throw new InvalidOperationException("The physical display returned an invalid environment code.");
            }

            Antilatency.Utils.SafeDispose(ref _environment);
            _environment = replacement;
            foreach (var stylus in _styluses.Values) {
                stylus.Dispose();
            }
            _styluses.Clear();
        }

        private void TryStartStyluses(INetwork network) {
            var trackingNodes = _trackingConstructor.findSupportedNodes(network);
            foreach (var extensionNode in _hardwareExtensionConstructor.findSupportedNodes(network)) {
                if (_styluses.ContainsKey(extensionNode.value) ||
                    network.nodeGetStatus(extensionNode) != NodeStatus.Idle ||
                    !IsStylusNode(network, extensionNode)) {
                    continue;
                }

                var trackingNode = trackingNodes.FirstOrDefault(node =>
                    network.nodeGetParent(node).value == extensionNode.value);
                if (trackingNode == NodeHandle.Null || network.nodeGetStatus(trackingNode) != NodeStatus.Idle) {
                    continue;
                }

                Antilatency.HardwareExtensionInterface.ICotask extensionCotask = null;
                IInputPin inputPin = null;
                ITrackingCotask trackingCotask = null;
                try {
                    extensionCotask = _hardwareExtensionConstructor.startTask(network, extensionNode);
                    inputPin = extensionCotask.createInputPin(Pins.IO1);
                    extensionCotask.run();
                    trackingCotask = _trackingConstructor.startTask(network, trackingNode, _environment);

                    var serial = SafeGetProperty(
                        network,
                        extensionNode,
                        Antilatency.DeviceNetwork.Interop.Constants.HardwareSerialNumberKey);
                    var id = string.IsNullOrWhiteSpace(serial)
                        ? $"node-{extensionNode.value}"
                        : serial;
                    _styluses.Add(
                        extensionNode.value,
                        new LocalStylus(id, extensionCotask, inputPin, trackingCotask));
                }
                catch (Exception) {
                    Antilatency.Utils.SafeDispose(ref inputPin);
                    Antilatency.Utils.SafeDispose(ref trackingCotask);
                    Antilatency.Utils.SafeDispose(ref extensionCotask);
                }
            }
        }

        private bool IsStylusNode(INetwork network, NodeHandle node) {
            var hardwareName = SafeGetProperty(
                network,
                node,
                Antilatency.DeviceNetwork.Interop.Constants.HardwareNameKey);
            if (!string.IsNullOrEmpty(_hardwareNameContains) &&
                hardwareName.IndexOf(_hardwareNameContains, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }

            var tag = SafeGetProperty(network, node, "Tag");
            return _stylusTags.Any(requiredTag =>
                !string.IsNullOrWhiteSpace(requiredTag) &&
                string.Equals(tag, requiredTag, StringComparison.Ordinal));
        }

        private void RemoveFinishedTasks() {
            if (!_displayCotask.IsNull() && _displayCotask.isTaskFinished()) {
                ResetDeviceTasks();
                return;
            }

            foreach (var key in _styluses
                         .Where(pair => pair.Value.IsFinished)
                         .Select(pair => pair.Key)
                         .ToArray()) {
                _styluses[key].Dispose();
                _styluses.Remove(key);
            }
        }

        private DisplayStylusFrame SampleFrame(float extrapolationSeconds) {
            var styluses = new List<DisplayStylusDeviceFrame>(_styluses.Count);
            foreach (var stylus in _styluses.Values) {
                var frame = stylus.Sample(extrapolationSeconds);
                if (frame != null) {
                    styluses.Add(frame);
                }
            }

            return new DisplayStylusFrame {
                Sequence = ++_sequence,
                Source = "local-adn",
                ReceivedAtRealtime = Time.realtimeSinceStartupAsDouble,
                Display = SampleDisplay(),
                Styluses = styluses
            };
        }

        private DisplayStylusDisplayFrame SampleDisplay() {
            if (_displayCotask.IsNull() || _displayCotask.isTaskFinished()) {
                return null;
            }

            var rotation = Quaternion.identity;
            IOrientationAwareEnvironment orientationAware = null;
            try {
                _environment?.QueryInterface(out orientationAware);
                if (!orientationAware.IsNull()) {
                    rotation = orientationAware.getRotation();
                }
            }
            finally {
                Antilatency.Utils.SafeDispose(ref orientationAware);
            }

            return new DisplayStylusDisplayFrame {
                Connected = true,
                ConfigId = _displayCotask.getConfigId(),
                ConfigCount = _displayCotask.getConfigCount(),
                ScreenPosition = _displayCotask.getScreenPosition(),
                ScreenX = _displayCotask.getScreenX(),
                ScreenY = _displayCotask.getScreenY(),
                EnvironmentRotation = rotation
            };
        }

        private void ResetDeviceTasks() {
            foreach (var stylus in _styluses.Values) {
                stylus.Dispose();
            }
            _styluses.Clear();
            Antilatency.Utils.SafeDispose(ref _environment);
            Antilatency.Utils.SafeDispose(ref _displayCotask);
            _latestFrame = null;
        }

        private void PublishDisconnectedFrame() {
            if (_latestFrame?.Display?.Connected == false && _latestFrame.Styluses.Count == 0) {
                return;
            }

            _latestFrame = new DisplayStylusFrame {
                Sequence = ++_sequence,
                Source = "local-adn",
                ReceivedAtRealtime = Time.realtimeSinceStartupAsDouble,
                Display = new DisplayStylusDisplayFrame { Connected = false },
                Styluses = Array.Empty<DisplayStylusDeviceFrame>()
            };
        }

        private void DisposeLibraries() {
            Antilatency.Utils.SafeDispose(ref _hardwareExtensionConstructor);
            Antilatency.Utils.SafeDispose(ref _hardwareExtensionLibrary);
            Antilatency.Utils.SafeDispose(ref _trackingConstructor);
            Antilatency.Utils.SafeDispose(ref _trackingLibrary);
            Antilatency.Utils.SafeDispose(ref _environmentSelectorLibrary);
            Antilatency.Utils.SafeDispose(ref _physicalEnvironmentConstructor);
            Antilatency.Utils.SafeDispose(ref _physicalEnvironmentLibrary);
        }

        private static string SafeGetProperty(INetwork network, NodeHandle node, string key) {
            try {
                return network.nodeGetStringProperty(node, key) ?? string.Empty;
            }
            catch (Exception) {
                return string.Empty;
            }
        }

        private static Pose IdentityPlacement() => Pose.identity;

        private sealed class LocalStylus : IDisposable {
            private readonly Antilatency.HardwareExtensionInterface.ICotask _extensionCotask;
            private readonly IInputPin _inputPin;
            private readonly ITrackingCotask _trackingCotask;

            public LocalStylus(
                string id,
                Antilatency.HardwareExtensionInterface.ICotask extensionCotask,
                IInputPin inputPin,
                ITrackingCotask trackingCotask) {
                Id = id;
                _extensionCotask = extensionCotask;
                _inputPin = inputPin;
                _trackingCotask = trackingCotask;
            }

            public string Id { get; }
            public bool IsFinished =>
                _extensionCotask.IsNull() || _inputPin.IsNull() || _trackingCotask.IsNull() ||
                _extensionCotask.isTaskFinished() || _trackingCotask.isTaskFinished();

            public DisplayStylusDeviceFrame Sample(float extrapolationSeconds) {
                if (IsFinished) {
                    return null;
                }

                try {
                    var state = _trackingCotask.getExtrapolatedState(
                        IdentityPlacement(),
                        extrapolationSeconds);
                    return new DisplayStylusDeviceFrame {
                        Id = Id,
                        Connected = true,
                        ButtonPressed = _inputPin.getState() == PinState.Low,
                        Pose = state.pose,
                        Velocity = state.velocity,
                        LocalAngularVelocity = state.localAngularVelocity,
                        TrackingStage = state.stability.stage.ToString(),
                        Stability = state.stability.value
                    };
                }
                catch (Exception) {
                    return null;
                }
            }

            public void Dispose() {
                _inputPin?.Dispose();
                _trackingCotask?.Dispose();
                _extensionCotask?.Dispose();
            }
        }
    }
}
