using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {
    public class StylusTipPointer : BaseStylusGrabPointer {

        [SerializeField] private Vector3 _triggerSize = Vector3.one;
        [SerializeField] private Vector3 _triggerOffset;
        [SerializeField] private bool _useOverlapBox;
        [SerializeField] private bool _detectOnlyNearestCollider = true;

        private List<Collider> _colliders = new();

        protected override void OnDisable() {
            base.OnDisable();

            foreach (var collider in _colliders) {
                HandleExitCollider(collider);
            }
        }

        private void FixedUpdate() {
            UpdateColliders(_useOverlapBox);
        }

        private void UpdateColliders(bool useOverlapBox) {

            if (GrabbingObject != null) {
                return;
            }

            var posTrigger = transform.position;
            var triggerSize = _triggerSize;
            triggerSize.Scale(transform.lossyScale);

            var hits = useOverlapBox ? new RaycastHit[0] : Physics.BoxCastAll(posTrigger, triggerSize * 0.5f, transform.forward, transform.rotation, 0.0001f);
            var currentColliders = useOverlapBox ? Physics.OverlapBox(posTrigger, triggerSize * 0.5f, transform.rotation) : new Collider[hits.Length];

            if (!useOverlapBox) {

                for (int i = 0; i < hits.Length; i++) {
                    currentColliders[i] = hits[i].collider;
                }
            }

            if (currentColliders.Length > 0) {
                currentColliders = currentColliders.OrderBy(c => (c.transform.position - transform.position).magnitude).ToArray();
            }

            var notFoundColliders = new List<Collider>();
            notFoundColliders.AddRange(_colliders);
            _colliders.Clear();

            if (!_detectOnlyNearestCollider) {
                for (int i = currentColliders.Length - 1; i >= 0; i--) {

                    var currentCollider = currentColliders[i] ?? hits[i].collider;
                    bool found = false;

                    for (int j = notFoundColliders.Count - 1; j >= 0; j--) {
                        var pCollider = notFoundColliders[j];

                        if (pCollider == currentCollider) {
                            found = true;
                            notFoundColliders.RemoveAt(j);
                            break;
                        }
                    }

                    _colliders.Add(currentCollider);

                    if (!found) {
                        ColliderWasEnter(currentCollider);
                    }
                    else {
                        ColliderStay(currentCollider);
                    }
                }
            }
            else {
                var nearestCollider = currentColliders.Length > 0 ? currentColliders[0] : null;
                bool found = false;

                if (nearestCollider != null) {
                    for (int j = notFoundColliders.Count - 1; j >= 0; j--) {
                        var pCollider = notFoundColliders[j];

                        if (pCollider == nearestCollider) {
                            found = true;
                            notFoundColliders.RemoveAt(j);
                        }
                    }

                    _colliders.Add(nearestCollider);

                    if (!found) {
                        ColliderWasEnter(nearestCollider);
                    }
                    else {
                        ColliderStay(nearestCollider);
                    }
                }
            }


            for (int i = notFoundColliders.Count - 1; i >= 0; i--) {
                Collider col = notFoundColliders[i];
                ColliderWasExit(col);
            }
        }

        private void ColliderWasEnter(Collider col) {
            AddOrUpdateCollider(col);
        }

        private void ColliderStay(Collider col) {
            AddOrUpdateCollider(col);
        }

        private void ColliderWasExit(Collider col) {
            HandleExitCollider(col);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.red;
            Vector3 posTrigger = transform.InverseTransformPoint(transform.position);
            Gizmos.DrawWireCube(posTrigger + _triggerOffset, _triggerSize);
            Gizmos.matrix = prevMatrix;
        }
#endif
    }
}