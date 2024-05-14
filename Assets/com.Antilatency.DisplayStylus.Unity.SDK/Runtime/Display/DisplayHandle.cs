using System;
using Antilatency.Alt.Environment;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Antilatency.DisplayStylus.SDK {
    [ExecuteAlways]
    public class DisplayHandle : MonoBehaviour{

        public static DisplayHandle Instance;

        [Range(-1, 1)] public int OriginX = 0;

        [Range(-1, 1)] public int OriginY = -1;

        public enum TScaleMode{
            RealSize,
            WidthIsOne,
            HeightIsOne,
        }

        public TScaleMode ScaleMode;

        public bool ShowDisplayBorder = true;
        public bool ShowRuler = false;
        private Display _display = null;

        private void Start(){
            FindDisplay();
        }

        private void OnEnable(){
            if (Instance != null){
                if (Application.isEditor || Debug.isDebugBuild){
                    Debug.LogError(
                        "Detected dublicate display handle. Will ignoring new instance display handle! Use one active display handle on scene!");
                }

                return;
            }

            Instance = this;
        }


        private void Update(){

            if (_display == null){
                if (!FindDisplay()){
                    return;
                }
            }

            var position = -_display.DisplayProperties.ScreenPosition - _display.DisplayProperties.ScreenX * OriginX -
                _display.DisplayProperties.ScreenY * OriginY;

            var scale = Vector3.one;
            if (ScaleMode != TScaleMode.RealSize){
                var size = _display.GetHalfScreenSize() * 2;
                if (ScaleMode == TScaleMode.WidthIsOne){
                    scale = Vector3.one / size.x;
                }
                else{
                    scale = Vector3.one / size.y;
                }
            }

            _display.transform.localScale = scale;
            position.Scale(scale);
            _display.transform.localPosition = position;
            
            var environment = _display.GetEnvironment();
            if (_display.SyncWithPhysicalDisplayRotation && !environment.IsNull()){
                _display.transform.localRotation =
                    environment.QueryInterface<IOrientationAwareEnvironment>().getRotation();
            }
            else{
                _display.transform.localRotation = Quaternion.identity;
            }
        }

        private bool FindDisplay(){
            _display = GetComponentInChildren<Display>();
            return _display != null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos(){
            if (_display == null){
                return;
            }

            var matrix = Handles.matrix;
            
            if (ShowDisplayBorder){
                if (!_display.Environment.IsNull()){
                    var environmentRotation = _display.Environment.QueryInterface<IOrientationAwareEnvironment>().getRotation();
                    Handles.matrix = _display.transform.localToWorldMatrix * QuaternionToMatrix(Quaternion.Inverse(environmentRotation));
                    
                    foreach (var i in _display.Environment.getMarkers()){
                        Handles.SphereHandleCap(0, i, Quaternion.identity, 0.005f, EventType.Repaint);
                    }
                }
            
                Handles.matrix = _display.transform.localToWorldMatrix;
                Handles.matrix *= _display.GetScreenToEnvironment();

                var halfScreenSize = _display.GetHalfScreenSize();
                Handles.DrawSolidRectangleWithOutline(new Rect(-halfScreenSize, 2 * halfScreenSize),
                    new Color(0, 0, 0, 0), Color.white);
            }

            if (ShowRuler){
                DrawRuler();
            }

            Handles.matrix = matrix;
        }

        private void DrawRuler(){
            var halfScreenSize = _display.GetHalfScreenSize();

            var rulerStart = new Vector3(0.0f, -halfScreenSize.y, 0.0f);
            const float rulerLength = 1.5f;
            var rulerEnd = rulerStart + Vector3.back * rulerLength;

            const float distanceBetweenMarks = 0.5f;
            var marksCount = (int)Mathf.Floor(rulerLength / distanceBetweenMarks);

            for (var i = 1; i <= marksCount; ++i){
                DrawRulerMark(rulerStart + Vector3.back * distanceBetweenMarks * i, distanceBetweenMarks * i);
            }

            Handles.DrawLine(rulerStart, rulerEnd);
        }

        private void DrawRulerMark(Vector3 markPosition, float distance){
            var markHalfSize = 0.025f;
            var markStart = markPosition - Vector3.right * markHalfSize;
            var markEnd = markPosition + Vector3.right * markHalfSize;
            Handles.DrawLine(markStart, markEnd);

            Handles.Label(markEnd + Vector3.right * 0.01f, distance.ToString("F2") + " m");
        }
#endif

        public static Matrix4x4 QuaternionToMatrix(Quaternion q)
        {
            float w = q.w;
            float x = q.x;
            float y = q.y;
            float z = q.z;
            float xx = x * x;
            float yy = y * y;
            float zz = z * z;
            float xy = x * y;
            float xz = x * z;
            float yz = y * z;
            float wx = w * x;
            float wy = w * y;
            float wz = w * z;
            Matrix4x4 matrix = new Matrix4x4();
            matrix.m00 = 1.0f - 2.0f * (yy + zz);
            matrix.m01 = 2.0f * (xy - wz);
            matrix.m02 = 2.0f * (xz + wy);
            matrix.m03 = 0.0f;
            matrix.m10 = 2.0f * (xy + wz);
            matrix.m11 = 1.0f - 2.0f * (xx + zz);
            matrix.m12 = 2.0f * (yz - wx);
            matrix.m13 = 0.0f;
            matrix.m20 = 2.0f * (xz - wy);
            matrix.m21 = 2.0f * (yz + wx);
            matrix.m22 = 1.0f - 2.0f * (xx + yy);
            matrix.m23 = 0.0f;
            matrix.m30 = 0.0f;
            matrix.m31 = 0.0f;
            matrix.m32 = 0.0f;
            matrix.m33 = 1.0f;
            return matrix;
        }
        
    }
}