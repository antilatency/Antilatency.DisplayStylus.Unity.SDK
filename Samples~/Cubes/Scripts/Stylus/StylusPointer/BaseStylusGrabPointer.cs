using UnityEngine;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {
    public abstract class BaseStylusGrabPointer : BaseStylusPointer {
        private IStylusPointerGrabbable? _grabbingObject;
        protected IStylusPointerGrabbable GrabbingObject => _grabbingObject;

        protected override void OnDisable() {
            base.OnDisable();
            _grabbingObject = null;
        }

        protected override void HandleExitObject(IStylusPointerHandler handler) {

            var obj = handler as MonoBehaviour;
            if (_grabbingObject != null && obj is IStylusPointerGrabbable grabbableObject) {

                if (_grabbingObject.ColliderForGrab != null && grabbableObject.ColliderForGrab == _grabbingObject.ColliderForGrab) {
                    HandleEndGrabObject(_grabbingObject);
                    _grabbingObject = null;
                }
            }

            base.HandleExitObject(handler);
        }

        protected override void HandleButtonPhaseDown(MonoBehaviour obj) {
            base.HandleButtonPhaseDown(obj);
            TryStartGrab(obj);
        }

        private void Update() {

            if (IsButtonPhaseDown) {
                if (_grabbingObject != null)
                    return;
                foreach (var kvp in _objects) {

                    if (!IsPressed(kvp.Key)) {
                        continue;
                    }

                    TryStartGrab(kvp.Value);

                    if (_grabbingObject != null) {
                        break;
                    }
                }
            }
        }

        private void TryStartGrab(MonoBehaviour obj) {

            var grabbable = obj as IStylusPointerGrabbable;

            if (grabbable != null && grabbable.IsAvaiableForGrab) {
                _grabbingObject = grabbable;
                HandleStartGrabObject(_grabbingObject);
            }
        }

        private void EndGrab() {

            if (_grabbingObject == null)
                return;


            HandleEndGrabObject(_grabbingObject);
            _grabbingObject = null;
        }

        protected override void HandleButtonPhaseUp(MonoBehaviour obj) {
            base.HandleButtonPhaseUp(obj);

            if (obj as IStylusPointerGrabbable == _grabbingObject) {
                EndGrab();
            }
        }

        protected virtual void HandleStartGrabObject(IStylusPointerGrabbable grabbableObject) {
            grabbableObject?.OnStartStylusPointerGrab(this);
        }

        protected virtual void HandleEndGrabObject(IStylusPointerGrabbable grabbableObject) {
            grabbableObject?.OnEndStylusPointerGrab(this);
        }

        protected override void HandleUpdateStylusPose(Pose stylusPose, Vector3 stylusWorldVelocity, Vector3 stylusAngularVelocity) {
            _grabbingObject?.OnStylusPointerGrabbing(this);
        }
    }
}