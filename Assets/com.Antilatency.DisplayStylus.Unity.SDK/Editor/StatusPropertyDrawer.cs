﻿using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Antilatency.DisplayStylus.SDK.Editor {
    [CustomPropertyDrawer(typeof(LifeTimeControllerStateMachine.TStatus))]
    public class StatusDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            //return EditorGUI.GetPropertyHeight(property, label, true);
            return 24;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label){
            
            var oldBackgroundColor = GUI.backgroundColor;

            LifeTimeControllerStateMachine.TStatus GetValue(object o) {
                var p = o.GetType().GetField(property.name);
                var v = p.GetValue(o);
                return (LifeTimeControllerStateMachine.TStatus)v;
            }
            

            LifeTimeControllerStateMachine.TStatus.TKind kind = default; 
            string description = "";

            var target = property.serializedObject.targetObject;
            var valueOfFirst = GetValue(target);
            var valueOfAll = property.serializedObject.targetObjects.Select(x => GetValue(x)).ToArray();

            if (valueOfAll.All(x => x.Kind == valueOfFirst.Kind)) {
                kind = valueOfFirst.Kind;
            }

            if (valueOfAll.All(x => x.Description == valueOfFirst.Description)) {
                description = valueOfFirst.Description;
            }
            else {
                description = "...";
            }

            switch (kind) {
                case LifeTimeControllerStateMachine.TStatus.TKind.Unknown:
                    GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
                    break;
                case LifeTimeControllerStateMachine.TStatus.TKind.Ok:
                    GUI.backgroundColor = new Color(0.4f, 1, 0.3f);
                    description = "Ok";
                    break;
                case LifeTimeControllerStateMachine.TStatus.TKind.Warning:
                    GUI.backgroundColor = Color.yellow;
                    break;
                case LifeTimeControllerStateMachine.TStatus.TKind.Error:
                    GUI.backgroundColor = new Color(1, 0.35f, 0.3f);
                    break;
            }

            GUI.Button(position, new GUIContent(description, description));
            GUI.backgroundColor = oldBackgroundColor;
        }
    }
}
