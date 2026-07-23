using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Antilatency.DisplayStylus.SDK {
    [RequireComponent(typeof(Display))]
    [RequireComponent(typeof(DisplayStylusConnection))]
    public class StylusesCreator : LifeTimeControllerStateMachine {
        [SerializeField] private GameObject stylusGoTemplate;

        public static StylusesCreator Instance => _instance;
        public static event Action<Stylus> OnCreatedStylus;
        public IReadOnlyList<Stylus> Styluses => _styluses;

        private static StylusesCreator _instance;
        private readonly List<Stylus> _styluses = new(2);
        private readonly Dictionary<string, Stylus> _stylusesBySourceId = new();
        private readonly HashSet<string> _activeSourceIds = new();
        private DisplayStylusConnection _connection;
        private Display _display;
        private int _nextStylusId;

#if UNITY_EDITOR
        private void Reset() {
            ValidateStylusTemplate(false);
        }

        private void OnValidate() {
            ValidateStylusTemplate(true);
        }

        public void ValidateStylusTemplate(bool showWarning = true) {
            if (stylusGoTemplate != null) {
                return;
            }

            if (showWarning) {
                Debug.LogWarning("Stylus template cannot be null.", this);
            }

            foreach (var rootFolder in new[] { "Assets", "Packages" }) {
                stylusGoTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{rootFolder}/com.Antilatency.Displaystylus.Unity.Sdk/StylusTemplate.prefab");
                if (stylusGoTemplate != null) {
                    break;
                }
            }

            if (stylusGoTemplate != null && stylusGoTemplate.GetComponent<Stylus>() == null) {
                Debug.LogError("The default stylus template does not contain Stylus.cs.", stylusGoTemplate);
            }
        }
#endif

        protected override void Create() {
            if (_instance != null && _instance != this) {
                Debug.LogError("Only one active StylusesCreator is supported. The duplicate will be removed.", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _connection = GetComponent<DisplayStylusConnection>();
            if (_connection == null) {
                _connection = gameObject.AddComponent<DisplayStylusConnection>();
            }
            _display = GetComponent<Display>();
            base.Create();
        }

        protected override IEnumerable StateMachine() {
            while (!Destroying) {
                var frame = _connection?.LatestFrame;
                if (frame == null) {
                    yield return _connection?.ConnectionStatus ?? "Waiting for DisplayStylusConnection";
                    continue;
                }

                ReconcileStyluses(frame.Styluses);
                yield return null;
            }
        }

        protected override void Destroy() {
            foreach (var stylus in _styluses.ToArray()) {
                if (stylus != null) {
                    stylus.OnDestroying -= OnDestroyingStylus;
                    stylus.Disconnect();
                }
            }

            _styluses.Clear();
            _stylusesBySourceId.Clear();
            _activeSourceIds.Clear();
            if (_instance == this) {
                _instance = null;
            }
            base.Destroy();
        }

        private void ReconcileStyluses(IReadOnlyList<DisplayStylusDeviceFrame> frames) {
            _activeSourceIds.Clear();

            for (var i = 0; i < frames.Count; i++) {
                var frame = frames[i];
                if (frame == null || !frame.Connected || string.IsNullOrEmpty(frame.Id)) {
                    continue;
                }

                _activeSourceIds.Add(frame.Id);
                if (!_stylusesBySourceId.TryGetValue(frame.Id, out var stylus) || stylus == null) {
                    stylus = CreateStylus(frame.Id);
                    if (stylus == null) {
                        continue;
                    }
                }

                stylus.SetFrame(frame);
            }

            foreach (var pair in new List<KeyValuePair<string, Stylus>>(_stylusesBySourceId)) {
                if (!_activeSourceIds.Contains(pair.Key)) {
                    pair.Value?.Disconnect();
                }
            }
        }

        private Stylus CreateStylus(string sourceId) {
            if (stylusGoTemplate == null) {
                Debug.LogError("Cannot create a stylus because the template is not assigned.", this);
                return null;
            }

            var instance = Instantiate(stylusGoTemplate, Vector3.zero, Quaternion.identity, transform);
            var stylus = instance.GetComponent<Stylus>();
            if (stylus == null) {
                Debug.LogError("The stylus template does not contain a Stylus component.", instance);
                Destroy(instance);
                return null;
            }

            stylus.Initialize(_nextStylusId++, sourceId, _display);
            stylus.OnDestroying += OnDestroyingStylus;
            _styluses.Add(stylus);
            _stylusesBySourceId[sourceId] = stylus;
            OnCreatedStylus?.Invoke(stylus);
            return stylus;
        }

        private void OnDestroyingStylus(Stylus stylus) {
            if (stylus == null) {
                return;
            }

            stylus.OnDestroying -= OnDestroyingStylus;
            _styluses.Remove(stylus);
            if (!string.IsNullOrEmpty(stylus.SourceId)) {
                _stylusesBySourceId.Remove(stylus.SourceId);
            }
        }
    }
}
