namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {
    public interface IStylusPointerClickHandler {
        void OnStylusButtonPhaseDown(BaseStylusPointer pointer);
        void OnStylusButtonPhaseUp(BaseStylusPointer pointer);
        void OnStylusButtonClicked(BaseStylusPointer pointer);
    }
}