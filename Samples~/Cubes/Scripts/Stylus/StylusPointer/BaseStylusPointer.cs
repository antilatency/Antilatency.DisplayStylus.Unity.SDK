using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {
    public abstract class BaseStylusPointer : MonoBehaviour {
        public Stylus Stylus => _stylus;
        public Rigidbody PhysicComponent => physicComponent;

        [SerializeField] protected Stylus _stylus;
        [SerializeField] protected Rigidbody physicComponent;

        /// <summary>
        /// Key is instance ID MonoBehaviour.
        /// </summary>
        protected Dictionary<long, MonoBehaviour> _objects = new Dictionary<long, MonoBehaviour>();

        protected bool IsButtonPhaseDown => _previousButtonPhase;
        private HashSet<long> _pressedObjects = new HashSet<long>();
        private bool _previousButtonPhase = false;

        protected virtual void OnEnable() {
            if (physicComponent) {
                physicComponent.isKinematic = true;
            }

            Stylus.OnUpdateButtonPhase += HandleButtonPhase;
            Stylus.OnUpdatedPose += HandleUpdateStylusPose;
        }

        protected virtual void OnDisable() {
            ReleaseAllObjects();
            _previousButtonPhase = false;
            Stylus.OnUpdateButtonPhase -= HandleButtonPhase;
            Stylus.OnUpdatedPose -= HandleUpdateStylusPose;
        }

        protected MonoBehaviour AddOrUpdateCollider(Collider col) {

            var obj = col.GetComponent<IStylusPointerHandler>() as MonoBehaviour;

            if (!obj)
                return null;

            long id = obj.GetInstanceID();

            if (!_objects.ContainsKey(id)) {
                HandleEnterObject((IStylusPointerHandler)obj);
            }

            _objects[id] = obj;
            return obj;
        }

        private void ReleaseAllObjects() {

            long[] keys = _objects.Keys.ToArray();
            foreach (var instanceId in keys) {
                HandleExitObjectInternal(_objects[instanceId]);
            }

            _objects.Clear();
            _pressedObjects.Clear();
        }

        protected bool IsPressed(long instanceId) {
            return _pressedObjects.Contains(instanceId);
        }

        protected void HandleExitCollider(Collider col) {

            var obj = col.GetComponent<IStylusPointerHandler>() as MonoBehaviour;

            if (!obj)
                return;

            HandleExitObjectInternal(obj);
        }

        private void HandleExitObjectInternal(MonoBehaviour obj) {

            long id = obj.GetInstanceID();
            if (_objects.ContainsKey(id)) {
               
                _objects.Remove(id);
                _pressedObjects.Remove(id);

                HandleExitObject((IStylusPointerHandler)obj);
            }
        }

        private void HandleButtonPhase(Stylus stylus, bool phase) {

            if (_previousButtonPhase != phase) {

                foreach (var kvp in _objects) {
                
                    if(phase) {
                        HandleButtonPhaseDown(kvp.Value);
                        _pressedObjects.Add(kvp.Key);
                    }
                    else {
                        HandleButtonPhaseUp(kvp.Value);

                        if (_pressedObjects.Contains(kvp.Key)) {
                            _pressedObjects.Remove(kvp.Key);
                            HandleButtonClick(kvp.Value);
                        }
                    }
                
                }
            }

            _previousButtonPhase = phase;
        }

        protected virtual void HandleEnterObject(IStylusPointerHandler handler) {
            handler?.OnStylusPointerEnter(this);
        }

        protected virtual void HandleExitObject(IStylusPointerHandler handler) {
            handler?.OnStylusPointerExit(this);
        }

        protected virtual void HandleButtonPhaseDown(MonoBehaviour obj) {
            (obj as IStylusPointerClickHandler)?.OnStylusButtonPhaseDown(this);
        }

        protected virtual void HandleButtonPhaseUp(MonoBehaviour obj) {
            (obj as IStylusPointerClickHandler)?.OnStylusButtonPhaseUp(this);
        }

        protected virtual void HandleButtonClick(MonoBehaviour obj) {
            (obj as IStylusPointerClickHandler)?.OnStylusButtonClicked(this);
        }

        protected abstract void HandleUpdateStylusPose(Pose stylusPose, Vector3 worldVelocity, Vector3 angularVelocity);

        public override int GetHashCode() {
            return Stylus.Id;
        }
    }
}