using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {
    public class StylusLaserPointer : BaseStylusGrabPointer {

        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private float maxDistance;
        private Dictionary<long, Collider> _colliders;
        private Dictionary<long, float> _objectIdToInstanceId;

        protected override void OnEnable() {
            base.OnEnable();

            _colliders = new Dictionary<long, Collider>();
            _objectIdToInstanceId = new Dictionary<long, float>();

            Application.onBeforeRender += OnBeforeRender;
        }

        protected override void OnDisable() {
            base.OnDisable();

            Application.onBeforeRender -= OnBeforeRender;
            RemoveAllNotFoundColliders(null);
        }

        private void OnBeforeRender() {
            UpdateLaser();
        }

        private void UpdateLaser() {

            Vector3 direction = transform.forward;
            Vector3 endPos = Vector3.zero;

            Ray ray = new Ray(transform.position, direction * maxDistance);

            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance);
            hits = hits.OrderBy(i => i.distance).ToArray();

            List<Collider> foundColliders = new ();

            if (GrabbingObject == null) {
                if (hits.Length > 0) {

                    RaycastHit hitInfo = hits[0];
                    endPos = hitInfo.point;

                    foundColliders.Add(hitInfo.collider);

                    _colliders[hitInfo.colliderInstanceID] = hitInfo.collider;
                    _objectIdToInstanceId[hitInfo.colliderInstanceID] = hitInfo.distance;

                    AddOrUpdateCollider(hitInfo.collider);
                    RemoveAllNotFoundColliders(foundColliders);
                }
                else {
                    RemoveAllNotFoundColliders(null);
                    endPos = transform.position + direction * maxDistance;
                }
            }
            else {
                endPos = transform.position + direction * _objectIdToInstanceId[GrabbingObject.ColliderForGrab.GetInstanceID()];
            }


            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, endPos);
        }


        private void RemoveAllNotFoundColliders(List<Collider> foundColliders) {
            List<long> remove = new();

            foreach (var kvp in _colliders) {
                bool found = false;

                if (foundColliders != null) {
                    found = foundColliders.Find(c => c.GetInstanceID() == kvp.Key) != null;
                }

                if (!found) {
                    remove.Add(kvp.Key);
                }
            }

            foreach (long instanceId in remove) {
                HandleExitCollider(_colliders[instanceId]);
                _colliders.Remove(instanceId);
                _objectIdToInstanceId.Remove(instanceId);
            }
        }
    }
}