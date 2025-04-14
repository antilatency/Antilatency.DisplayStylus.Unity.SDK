using System.Collections.Generic;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK.Samples.Cubes {

    public class StylusPointerSwitcher : MonoBehaviour {
        [SerializeField] private Stylus stylus;
        [SerializeField] private List<BaseStylusPointer> variants = new ();
        [SerializeField] private float maxPressTimeForDetectClick = 0.5f;
        [SerializeField, Header("Key P (EN)")] private bool switchViaKeyboardOnly;

        private int _currentIndex = 0;
        private bool _previousPhaseStylusButton;
        private float _startPressButtonTime;

        private void OnEnable() {

            stylus.OnUpdateButtonPhase += OnUpdatedButtonPhase;

            foreach (var visual in variants) {
                visual.gameObject.SetActive(false);
            }

            UpdateActiveVariants();
        }

        private void OnDisable() {
            stylus.OnUpdateButtonPhase -= OnUpdatedButtonPhase;
        }

        private void OnUpdatedButtonPhase(Stylus stylus, bool isPressed) {
            if (_previousPhaseStylusButton != isPressed) {
                if (isPressed) {
                    //Button down phase
                    _startPressButtonTime = Time.time;
                }
                else {
                    //Click
                    if (maxPressTimeForDetectClick > Time.time - _startPressButtonTime) {
                        if (!switchViaKeyboardOnly) {
                            Next();
                        }
                    }
                }
            }

            _previousPhaseStylusButton = isPressed;
        }

        public void Next() {
            _currentIndex++;
            if (_currentIndex >= variants.Count) {
                _currentIndex = 0;
            }

            UpdateActiveVariants();
        }

        private void UpdateActiveVariants() {
            for (int i = 0; i < variants.Count; i++) {
                var variant = variants[i];
                bool isActive = _currentIndex == i;
                variant.gameObject.SetActive(isActive);
            }
        }

        private void Update() {
            if (Input.GetKeyDown(KeyCode.P)) {
                Next();
            }
        }
    }
}