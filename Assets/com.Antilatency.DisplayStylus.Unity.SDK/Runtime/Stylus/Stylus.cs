using System;
using System.Collections;
using Antilatency.Alt.Tracking;
using Antilatency.HardwareExtensionInterface;
using Antilatency.HardwareExtensionInterface.Interop;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK
{
    public class Stylus : LifeTimeControllerStateMachine
    {
        /// <summary>
        /// bool - If true, button pressed, else unpressed.
        /// </summary>
        public event Action<Stylus,bool> OnUpdateButtonPhase;
        /// <summary>
        /// Pose - Extrapolated world space pose.
        /// Vector3 - Extrapolated world space velocity.
        /// Vector3 - Extrapolated world space angular velocity.
        /// </summary>
        public event Action<Pose, Vector3,Vector3> OnUpdatedPose;
        public event Action<Stylus> OnDestroying;
        public int Id => _id;
        
        /// <summary>
        /// In World Space.
        /// </summary>
        public Pose ExtrapolatedPose => _extrapolatedPose;
        /// <summary>
        /// In World Space.
        /// </summary>
        public Vector3 ExtrapolatedVelocity => _extrapolatedVelocity;
        /// <summary>
        /// In World Space.
        /// </summary>
        public Vector3 ExtrapolatedAngularVelocity => _extrapolatedAngularVelocity;
        public float ExtrapolationTime = 0.042f;
        
        protected ITrackingCotask _trackingCotask;
        protected ICotask _extensionCotask;
        protected IInputPin _inputPin;
        
        private int _id;
        private Display _display;
        private Pose _extrapolatedPose;
        private Vector3 _extrapolatedVelocity;
        private Vector3 _extrapolatedAngularVelocity;

        internal void Initialize(int id, Display display, ITrackingCotask trackingCotask, ICotask extensionsCotask,
            IInputPin inputPin){
            _display = display;
            _id = id;
            _trackingCotask = trackingCotask;
            _extensionCotask = extensionsCotask;
            _inputPin = inputPin;
        }

        protected override IEnumerable StateMachine(){
            if (!Destroying){
                Application.onBeforeRender += OnBeforeRenderer;
            }

            string status = "Waiting cotasks.";

            WaitCotasks:
            if (Destroying) yield break;

            if (_trackingCotask.IsNull() || _extensionCotask.IsNull() || _inputPin.IsNull()){
                yield return status;
                goto WaitCotasks;
            }

            status = string.Empty;
            while (!_inputPin.IsNull() && !_trackingCotask.IsNull() && !_extensionCotask.IsNull() &&
                   !_trackingCotask.isTaskFinished() &&
                   !_extensionCotask.isTaskFinished()){
                
                if (Destroying) yield break;
                
                OnUpdateButtonPhase?.Invoke(this, _inputPin.getState() == PinState.Low);
                yield return status;
            }

            Destroy(gameObject);
        }

        [BeforeRenderOrder(-29999)]
        private void OnBeforeRenderer()
        {
            UpdateStylusPose(ExtrapolationTime);
        }
        
        private void UpdateStylusPose(float time)
        {
            if (_trackingCotask.IsNull() || _trackingCotask.isTaskFinished())
            {
                return;
            }
            
            State extrapolatedState = _trackingCotask.getExtrapolatedState(Pose.identity, time);
            Pose extrapolatedPose = extrapolatedState.pose;
            
            var inverseEnvironmentRotation = Quaternion.Inverse(_display.EnvironmentRotation);
            var toInverseEnvironmentMatrix = Math.QuaternionToMatrix(inverseEnvironmentRotation);
            
            transform.localPosition = toInverseEnvironmentMatrix.MultiplyPoint(extrapolatedPose.position);
            transform.localRotation = extrapolatedPose.rotation * toInverseEnvironmentMatrix.rotation;
            
            Transform displayHandleT = StylusesCreator.Instance.transform;
            Vector3 worldVelocity = displayHandleT.TransformVector(toInverseEnvironmentMatrix.MultiplyVector(extrapolatedState.velocity));
            Vector3 angularVelocity = displayHandleT.TransformDirection(extrapolatedState.localAngularVelocity);

            _extrapolatedPose = new Pose(transform.position, transform.rotation);
            _extrapolatedVelocity = worldVelocity;
            _extrapolatedAngularVelocity = angularVelocity;

            OnUpdatedPose?.Invoke(ExtrapolatedPose, ExtrapolatedVelocity, ExtrapolatedAngularVelocity);
        }

        protected override void Destroy()
        {
            base.Destroy();

            Application.onBeforeRender -= OnBeforeRenderer;
            Antilatency.Utils.SafeDispose(ref _inputPin);
            Antilatency.Utils.SafeDispose(ref _extensionCotask);
            Antilatency.Utils.SafeDispose(ref _trackingCotask);
            
            OnUpdateButtonPhase?.Invoke(this,false);
            OnDestroying?.Invoke(this);
        }
    }
}