# Antilatency.Styluses.Unity.SDK

## Setup

### Add Antilatency SDK
Add the following URL to your Package Manager:
```
https://github.com/AntilatencySDK/Release_4.4.0.git#subset-41ab5faa283aabb0d73770a976e6cb8b9339421f
```

### Add Antilatency.DisplayStylus.Unity.SDK
Add the following URL to your Package Manager:
```
https://github.com/antilatency/Antilatency.DisplayStylus.Unity.SDK.git?path=/Assets/com.Antilatency.DisplayStylus.Unity.SDK#1.2.0
```

## Adding DisplayStylus to the Scene
In the top Unity menu, select **Display Stylus -> Create In Scene**.

## Display

The Display component is responsible for connecting to the controller that manages the markers.

### Sync With Physical Display Rotation
Applies rotation to the virtual screen based on the tilt of the physical one **when markers are visible to the Stylus (Alt device)**. In Editor Mode, this always works as false.

### Display Properties

For operation in Editor Mode, you can manually set parameters. In Play Mode, when the device (AntilatencyPhysicalConfigurableEnvironment) is connected, the parameters will update based on the device data.

- **Screen Position**: Position of the screen relative to the Environment (in meters).
- **ScreenX**: X-axis of the environment (screen width in meters / 2f).
- **ScreenY**: Y-axis of the environment (screen height in meters / 2f).

## Display Handle

The Display Handle is the parent object for the Display. It can be rotated, moved, and scaled.

- **Origin X**: Sets the origin point for the virtual display on the X-axis.
- **Origin Y**: Sets the origin point for the virtual display on the Y-axis.
- **Scale Mode**: Sets the scale mode in which the virtual display will operate.
- **Show Display Border**: Shows the borders of the virtual display.
- **Show Ruler**: Shows a ruler.

## Device Network

[Device Network](https://developers.antilatency.com/Terms/Antilatency_Device_Network_en.html) is the communication link between the application and connected Antilatency devices. It helps to monitor changes in connected devices and provides access to the [nodes](https://developers.antilatency.com/Terms/Node_en.html) of the [device tree](https://developers.antilatency.com/Terms/Antilatency_Device_Network_en.html#Device_tree).

## Styluses Creator

Styluses Creator finds stylus nodes and creates Stylus GameObjects based on the number of connected devices.

### Parameters

- **Stylus GO Template**: Contains the stylus prefab, which can have its own logic. The prefab must contain the Stylus component.
- **Required Tags**: Allows adding custom styluses (assembled by the user) based on their Tag properties. These values will be used to search for custom stylus nodes.

### About Custom Styluses

An unlimited number of styluses can be connected. A custom stylus can be assembled, for example, using a Hardware Extension Module and a socket with a tracker. The stylus must have a non-empty Tag property, which can be set in AntilatencyService. In the "Styluses Creator" component, the same Tag must be added to Required Tags for the stylus to be found by the application.

## Stylus

It's **important to know** this when testing in the Unity Editor. The position of the styluses is updated in Application.onBeforeRender. Make sure that the **Game Viewport is open and active**, as Unity will not call Application.onBeforeRender otherwise.

- `event Action<Stylus, bool> OnUpdatedButtonPhase`: Called with each new state update, even if the state stays the same. If the stylus is disconnected while the button is pressed, it signals the released state.
- `public event Action<Pose, Vector3, Vector3> OnUpdatedPose`: Called after the stylus pose is updated, with the following parameters:
  - **Pose**: The position and rotation of the stylus in world space.
  - **world velocity**: The velocity in world space.
  - **angular velocity**: The angular velocity.
- `event Action<Stylus> OnDestroying`: Called when the stylus GameObject is deleted.
- `Pose ExtrapolatedPose { get; }`: The last extrapolated pose received.
- `Vector3 ExtrapolatedVelocity { get; }`: The last extrapolated world velocity value received.
- `Vector3 ExtrapolatedAngularVelocity { get; }`: The last extrapolated angular velocity value received.

---

For more detailed information, refer to the official [Antilatency documentation](https://antilatency.com).
