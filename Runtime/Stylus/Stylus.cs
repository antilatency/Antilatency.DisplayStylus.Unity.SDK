using System;
using System.Collections;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    public class Stylus : LifeTimeControllerStateMachine {
        /// <summary>True while the stylus button is pressed.</summary>
        public event Action<Stylus, bool> OnUpdateButtonPhase;

        /// <summary>Extrapolated world-space pose, velocity, and angular velocity.</summary>
        public event Action<Pose, Vector3, Vector3> OnUpdatedPose;

        public event Action<Stylus> OnDestroying;

        public int Id => _id;
        public string SourceId => _sourceId;
        public Pose ExtrapolatedPose => _extrapolatedPose;
        public Vector3 ExtrapolatedVelocity => _extrapolatedVelocity;
        public Vector3 ExtrapolatedAngularVelocity => _extrapolatedAngularVelocity;

        // Kept for source compatibility. Extrapolation is performed by DisplayStylusConnection.
        public float ExtrapolationTime = 0.042f;

        private int _id;
        private string _sourceId;
        private Display _display;
        private DisplayStylusDeviceFrame _frame;
        private Pose _extrapolatedPose;
        private Vector3 _extrapolatedVelocity;
        private Vector3 _extrapolatedAngularVelocity;
        private bool _disconnecting;

        internal void Initialize(int id, string sourceId, Display display) {
            _id = id;
            _sourceId = sourceId;
            _display = display;
        }

        internal void SetFrame(DisplayStylusDeviceFrame frame) {
            _frame = frame;
        }

        internal void Disconnect() {
            if (_disconnecting) {
                return;
            }

            _disconnecting = true;
            Destroy(gameObject);
        }

        protected override IEnumerable StateMachine() {
            Application.onBeforeRender += OnBeforeRenderer;

            while (!Destroying && !_disconnecting) {
                if (_frame == null || !_frame.Connected) {
                    yield return "Waiting for stylus data";
                    continue;
                }

                OnUpdateButtonPhase?.Invoke(this, _frame.ButtonPressed);
                yield return null;
            }
        }

        [BeforeRenderOrder(-29999)]
        private void OnBeforeRenderer() {
            UpdateStylusPose();
        }

        private void UpdateStylusPose() {
            var frame = _frame;
            if (frame == null || !frame.Connected || _display == null) {
                return;
            }

            var inverseEnvironmentRotation = Quaternion.Inverse(_display.EnvironmentRotation);
            transform.localPosition = inverseEnvironmentRotation * frame.Pose.position;
            transform.localRotation = inverseEnvironmentRotation * frame.Pose.rotation;

            var displayHandle = transform.parent;
            var worldVelocity = displayHandle != null
                ? displayHandle.TransformVector(inverseEnvironmentRotation * frame.Velocity)
                : inverseEnvironmentRotation * frame.Velocity;
            var worldAngularVelocity = displayHandle != null
                ? displayHandle.TransformDirection(frame.LocalAngularVelocity)
                : frame.LocalAngularVelocity;

            _extrapolatedPose = new Pose(transform.position, transform.rotation);
            _extrapolatedVelocity = worldVelocity;
            _extrapolatedAngularVelocity = worldAngularVelocity;
            OnUpdatedPose?.Invoke(_extrapolatedPose, _extrapolatedVelocity, _extrapolatedAngularVelocity);
        }

        protected override void Destroy() {
            Application.onBeforeRender -= OnBeforeRenderer;
            OnUpdateButtonPhase?.Invoke(this, false);
            OnDestroying?.Invoke(this);
            base.Destroy();
        }
    }
}
