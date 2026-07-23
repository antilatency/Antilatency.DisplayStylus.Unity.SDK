using System.Collections;
using Antilatency.Alt.Environment;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    [RequireComponent(typeof(DisplayStylusConnection))]
    public class Display : LifeTimeControllerStateMachine, IEnvironmentProvider {
        public bool SyncWithPhysicalDisplayRotation;

        public Vector3 ScreenPosition = Vector3.zero;
        public Vector3 ScreenX = new(0.1505f, 0, 0);
        public Vector3 ScreenY = new(0, 0.095f, 0);

        public Quaternion EnvironmentRotation { get; private set; } = Quaternion.identity;

        private DisplayStylusConnection _connection;

        protected override IEnumerable StateMachine() {
            while (!Destroying) {
                if (_connection == null) {
                    _connection = GetComponent<DisplayStylusConnection>();
                    if (_connection == null) {
                        _connection = gameObject.AddComponent<DisplayStylusConnection>();
                    }
                }

                var displayFrame = _connection?.LatestFrame?.Display;
                if (displayFrame?.Connected == true) {
                    ScreenPosition = displayFrame.ScreenPosition;
                    ScreenX = displayFrame.ScreenX;
                    ScreenY = displayFrame.ScreenY;
                    EnvironmentRotation = IsNormalized(displayFrame.EnvironmentRotation)
                        ? displayFrame.EnvironmentRotation
                        : Quaternion.identity;

                    yield return null;
                }
                else {
                    EnvironmentRotation = Quaternion.identity;
                    yield return _connection?.ConnectionStatus ?? "Waiting for DisplayStylusConnection";
                }
            }
        }

        public Vector2 GetHalfScreenSize() {
            return new Vector2(ScreenX.magnitude, ScreenY.magnitude);
        }

        public Matrix4x4 GetScreenToEnvironment() {
            var x = ScreenX.normalized;
            var y = ScreenY.normalized;
            Vector4 w = ScreenPosition;
            w.w = 1;
            return new Matrix4x4(x, y, Vector3.Cross(x, y), w);
        }

        /// <summary>
        /// Returns the native environment in LocalAdn mode. Proxy mode intentionally returns null,
        /// because native Antilatency interfaces cannot cross a process boundary.
        /// </summary>
        public IEnvironment GetEnvironment() {
            return _connection?.LocalEnvironment;
        }

        private static bool IsNormalized(Quaternion value) {
            var lengthSquared =
                (double)value.x * value.x +
                (double)value.y * value.y +
                (double)value.z * value.z +
                (double)value.w * value.w;
            return System.Math.Abs(lengthSquared - 1.0) < 1e-6;
        }
    }
}
