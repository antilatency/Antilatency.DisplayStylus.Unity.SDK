using Antilatency.DisplayStylus.SDK;
using UnityEngine;

namespace Test{
    public class DrawStylusVelocity : MonoBehaviour{

        private Stylus _stylus;

        private void OnEnable(){
            _stylus = GetComponent<Stylus>();
        }

        private void Update(){
            Debug.DrawRay(_stylus.ExtrapolatedPose.position, _stylus.ExtrapolatedVelocity, Color.red);
        }
    }
}