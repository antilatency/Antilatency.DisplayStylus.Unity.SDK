using System;
using System.Collections.Generic;
using Antilatency.Alt.Environment;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    public sealed class DisplayStylusFrame {
        public long Sequence { get; internal set; }
        public string Source { get; internal set; }
        public double ReceivedAtRealtime { get; internal set; }
        public DisplayStylusDisplayFrame Display { get; internal set; }
        public IReadOnlyList<DisplayStylusDeviceFrame> Styluses { get; internal set; } = Array.Empty<DisplayStylusDeviceFrame>();
    }

    public sealed class DisplayStylusDisplayFrame {
        public bool Connected { get; internal set; }
        public uint ConfigId { get; internal set; }
        public uint ConfigCount { get; internal set; }
        public Vector3 ScreenPosition { get; internal set; }
        public Vector3 ScreenX { get; internal set; }
        public Vector3 ScreenY { get; internal set; }
        public Quaternion EnvironmentRotation { get; internal set; } = Quaternion.identity;
    }

    public sealed class DisplayStylusDeviceFrame {
        public string Id { get; internal set; }
        public bool Connected { get; internal set; }
        public bool ButtonPressed { get; internal set; }
        public Pose Pose { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public Vector3 LocalAngularVelocity { get; internal set; }
        public string TrackingStage { get; internal set; }
        public float Stability { get; internal set; }
    }

    internal interface IDisplayStylusDataSource : IDisposable {
        string Status { get; }
        IEnvironment LocalEnvironment { get; }
        void Tick(float extrapolationSeconds);
        bool TryGetLatestFrame(out DisplayStylusFrame frame);
    }
}
