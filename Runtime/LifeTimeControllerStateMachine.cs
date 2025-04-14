using System;
using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Antilatency.DisplayStylus.SDK {
    public abstract class LifeTimeControllerStateMachine : LifeTimeController {

        [Serializable]
        public class TStatus {
            public enum TKind {
                Unknown,
                Ok,
                Warning,
                Error
            }
         
            public TKind Kind;
            public string Description;
        }

        //private TStatus status;
        public TStatus Status;

        private void SetStatus(TStatus status) {
            Status = status;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }


        protected IEnumerator enumerator;

        protected override void Create() {
            enumerator = StateMachine().GetEnumerator();
        }

        protected bool Destroying { get; private set; }

        protected override void Destroy() {
            Destroying = true;

            int iterations = 1024;
            while (Tick()) {
                if (iterations <= 0)
                    throw new Exception("StateMachine does not exit. Check 'Destroing' field in every loop: if (Destroing) yield break;");
                iterations--;
            }

            Status = default;
            Destroying = false;
        }


        /*public virtual bool Tick() {        
            return enumerator.MoveNext();
        }*/


        public bool Tick() {
            if (enumerator == null) {
                return false;
            }
            try {

                var result = enumerator.MoveNext();

                var description = enumerator?.Current?.ToString();
                if (!string.IsNullOrEmpty(description)) {
                    SetStatus(new TStatus { Kind = TStatus.TKind.Warning, Description = description });
                }
                else {
                    SetStatus(Status = new TStatus { Kind = TStatus.TKind.Ok });
                }

                return result;
            }
            catch (Exception ex) {
                SetStatus(new TStatus { Kind = TStatus.TKind.Error, Description = ex.Message });
                enumerator = null;
                Debug.LogError(ex);
            }

            return false;
        }

        public virtual void Update() {

            Tick();
        }

        public virtual void FixedUpdate() {

        }

        protected abstract IEnumerable StateMachine();


    }
}