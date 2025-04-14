using UnityEngine;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {
    public interface IStylusPointerGrabbable {
        Collider ColliderForGrab { get; }
        bool IsAvaiableForGrab { get; }
        void OnStartStylusPointerGrab(BaseStylusGrabPointer pointer);
        void OnStylusPointerGrabbing(BaseStylusGrabPointer pointer);
        void OnEndStylusPointerGrab(BaseStylusGrabPointer ointer);
    }
}