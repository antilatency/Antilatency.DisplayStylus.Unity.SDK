using System.Collections.Generic;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {

    [RequireComponent(typeof(Rigidbody))]
    public class Cube : MonoBehaviour, IStylusPointerHandler, IStylusPointerGrabbable {

        [SerializeField] private Collider colliderForGrab;

        public Collider ColliderForGrab => colliderForGrab;
        public bool IsAvaiableForGrab => _isGrabbing == false;

        private HashSet<BaseStylusPointer> _pointers = new();
        private Rigidbody _rb;
        private FixedJoint _joint;
        private Vector3 _startPos;
        private Quaternion _startRot;
        private Vector3 _lastGrabVelocity;
        private Vector3 _lastGrabAngularVelocity;
        private bool _isGrabbing;

        private void Awake() {
            _startPos = transform.position;
            _startRot = transform.rotation;
        }

        private void OnEnable() {
            if (_rb == null) {
                _rb = GetComponent<Rigidbody>();
            }
        }

        public void ResetPoseCube() {
            if (_rb != null) {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            transform.position = _startPos;
            transform.rotation = _startRot;
        }

        public void OnStartStylusPointerGrab(BaseStylusGrabPointer pointer) {
            _joint = _joint ?? gameObject.AddComponent<FixedJoint>();
            _joint.connectedBody = pointer.PhysicComponent;
            _rb.useGravity = false;
            _isGrabbing = true;
        }

        public void OnStylusPointerGrabbing(BaseStylusGrabPointer pointer) {

        }

        public void OnEndStylusPointerGrab(BaseStylusGrabPointer pointer) {

            _lastGrabVelocity = pointer.Stylus.ExtrapolatedVelocity;
            _lastGrabAngularVelocity = pointer.Stylus.ExtrapolatedAngularVelocity;

            if (_joint) {
                Destroy(_joint);
                _joint = null;
            }

            _rb.useGravity = true;
            _rb.velocity = _lastGrabVelocity;
            _rb.angularVelocity = _lastGrabAngularVelocity;
            _isGrabbing = false;
        }

        public void OnStylusPointerEnter(BaseStylusPointer pointer) {
            _pointers.Add(pointer);
        }

        public void OnStylusPointerExit(BaseStylusPointer pointer) {
            _pointers.Remove(pointer);
        }

        private void Update() {
            if (Input.GetKeyDown(KeyCode.R)) {
                ResetPoseCube();
            }
        }
    }
}