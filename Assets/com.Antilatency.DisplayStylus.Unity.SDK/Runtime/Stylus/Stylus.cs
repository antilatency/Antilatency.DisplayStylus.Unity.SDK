using System;
using System.Collections;
using Antilatency.Alt.Tracking;
using Antilatency.HardwareExtensionInterface;
using Antilatency.HardwareExtensionInterface.Interop;
using UnityEngine;
using Antilatency;

namespace Antilatency.DisplayStylus
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
        public Pose ExtrapolatedPose => _extrapolatedPose;
        public Vector3 ExtrapolatedVelocity => _extrapolatedVelocity;
        public Vector3 ExtrapolatedAngularVelocity => _extrapolatedAngularVelocity;
       
        protected ITrackingCotask _trackingCotask;
        protected ICotask _extensionCotask;
        protected IInputPin _inputPin;
        private int _id;
        private Pose _extrapolatedPose;
        private Vector3 _extrapolatedVelocity;
        private Vector3 _extrapolatedAngularVelocity;

        public float ExtrapolationTime
        {
            get
            {
                return 0.042f;
            }
        }

        internal void Initialize(int id,ITrackingCotask trackingCotask, ICotask extensionsCotask,
            IInputPin inputPin)
        {
            _id = id;
            _trackingCotask = trackingCotask;
            _extensionCotask = extensionsCotask;
            _inputPin = inputPin;
        }

        protected override IEnumerable StateMachine()
        {
            if (!Destroying)
            {
                Subscribe();
            }
            
            string status = "Waiting cotasks.";

            WaitCotasks:
            if (Destroying) yield break;

            if (_trackingCotask.IsNull() || _extensionCotask.IsNull() || _inputPin.IsNull())
            {
                yield return status;
                goto WaitCotasks;
            }

            status = string.Empty;
            while (!_inputPin.IsNull() && !_trackingCotask.IsNull() && !_extensionCotask.IsNull() && !_trackingCotask.isTaskFinished() &&
                   !_extensionCotask.isTaskFinished())
            {
                OnUpdateButtonPhase?.Invoke(this, _inputPin.getState() == PinState.Low);
                if (Destroying)
                {
                    Debug.LogWarning("Stylus has been destroyed! Will call dispose all cotasks and pin.");
                    yield break;
                }
                else yield return status;
            }

            UnSubscribeAndDisposeAll();
            DestroyGameObject();
        }
        
        private void OnBeforeRendererLeft()
        {
            UpdateStylusPose(ExtrapolationTime);
        }

        private void OnBeforeRendererRight()
        { 
            UpdateStylusPose(ExtrapolationTime);
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

            transform.localPosition = extrapolatedPose.position;
            transform.localRotation = extrapolatedPose.rotation;

            Transform displayHandleT = StylusesCreator.Instance.transform;//DisplayHandle.Instance.transform;
            Vector3 worldVelocity = displayHandleT.TransformVector(extrapolatedState.velocity);
            Vector3 angularVelocity = displayHandleT.TransformDirection(extrapolatedState.localAngularVelocity);

            Pose worldPose = new Pose(transform.position, transform.rotation);

            _extrapolatedPose = worldPose;
            _extrapolatedVelocity = worldVelocity;
            _extrapolatedAngularVelocity = angularVelocity;

            OnUpdatedPose?.Invoke(ExtrapolatedPose, ExtrapolatedVelocity, ExtrapolatedAngularVelocity);
        }

        private void Subscribe()
        {
            Application.onBeforeRender += OnBeforeRenderer;
        }

        private void UnSubscribeAll()
        {
            Application.onBeforeRender -= OnBeforeRenderer;
        }

        private void UnSubscribeAndDisposeAll()
        {
            UnSubscribeAll();
            Antilatency.Utils.SafeDispose(ref _inputPin);
            Antilatency.Utils.SafeDispose(ref _extensionCotask);
            Antilatency.Utils.SafeDispose(ref _trackingCotask);
        }

        private void DestroyGameObject()
        {
            Destroy(gameObject);
        }

        protected override void Destroy()
        {
            base.Destroy();

            UnSubscribeAndDisposeAll();
            OnUpdateButtonPhase?.Invoke(this,false);
            OnDestroying?.Invoke(this);
        }
    }
}