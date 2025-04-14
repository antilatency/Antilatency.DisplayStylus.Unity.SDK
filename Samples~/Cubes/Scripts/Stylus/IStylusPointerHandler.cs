using UnityEngine;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {
    public interface IStylusPointerHandler {
        void OnStylusPointerEnter(BaseStylusPointer pointer);
        void OnStylusPointerExit(BaseStylusPointer pointer);
    }
}