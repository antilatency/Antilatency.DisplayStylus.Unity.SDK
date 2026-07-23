# Antilatency Display Stylus Unity SDK

Unity integration for Antilatency display styluses with one switchable
connection gateway:

- **Local ADN** creates the Antilatency Device Network and runs device cotasks
  inside Unity.
- **Proxy** receives display and stylus data from the standalone
  Antilatency Display Stylus Proxy.

The package supports Unity 2021.3.5f1 and later and depends on Antilatency SDK
4.6.0.

## Setup

### Add Antilatency SDK

In Unity Package Manager, select **Add package from git URL** and paste
[https://github.com/AntilatencySDK/Release_4.6.0.git#subset-22a756f0fb8b2c7a4dbd47324fdc939eb60f8372](https://github.com/AntilatencySDK/Release_4.6.0.git#subset-22a756f0fb8b2c7a4dbd47324fdc939eb60f8372).

This Antilatency SDK 4.6.0 subset includes every module required by the package,
including `AltEnvironmentSides`.

### Add this package

In Unity Package Manager, select **Add package from git URL** and paste
[https://github.com/antilatency/Antilatency.DisplayStylus.Unity.SDK.git#2.0.0](https://github.com/antilatency/Antilatency.DisplayStylus.Unity.SDK.git#2.0.0).

Use **Add package from disk** only when developing a local checkout of this
repository.

### Add samples

Open the package in Unity Package Manager and import the **Cubes** sample for
stylus interaction, pointer, and grabbing examples.

## Add Display Stylus to a scene

In the Unity top menu, select **Display Stylus > Create In Scene**. The command
creates:

- `DisplayHandle`
- `DisplayStylusConnection`
- `Display`
- `StylusesCreator`

Select `LocalAdn` or `Proxy` on `DisplayStylusConnection`. The mode can also be
changed at runtime.

Existing scenes created with package 1.2.x remain compatible. If a scene
contains `Display` and `Antilatency.SDK.DeviceNetwork` but no
`DisplayStylusConnection`, the connection is added automatically in `LocalAdn`
mode at runtime.

## Proxy mode

For the proxy release, startup instructions, Unity configuration, C# examples,
write leases, errors, protocol, and troubleshooting, see the
[Antilatency Display Stylus Proxy documentation](https://github.com/antilatency/Antilatency.DisplayStylus.Proxy#unity-client).

## Components

### DisplayStylusConnection

Selects local ADN or the proxy and exposes:

- `Mode`
- `LatestFrame`
- `FrameUpdated`
- `ConnectionStatus`
- `IsReady`
- `LocalEnvironment`
- `ExtrapolationSeconds`
- `CreateProxyWriter(clientId)`

In `LocalAdn` mode, the connection owns
`Antilatency.SDK.DeviceNetwork`, display cotask, tracking cotasks, and
hardware-extension cotasks. **Stylus Tags** and **Hardware Name Contains**
configure custom stylus discovery.

In `Proxy` mode, the component does not create a local ADN. Native interfaces
such as `INetwork`, `ICotask`, and `IEnvironment` cannot cross the process
boundary, so `LocalEnvironment` is `null`.

### Display

Connects virtual content to the controller managing the physical display
markers.

**Sync With Physical Display Rotation** applies the physical display tilt to the
virtual screen when its markers are visible to the stylus tracking device. It
is inactive outside Play Mode.

For Editor layout work, screen parameters can be set manually. In Play Mode,
they update from the selected connection:

- **Screen Position**: screen position relative to the Antilatency environment,
  in meters.
- **Screen X**: environment-space X axis whose magnitude is half the screen
  width in meters.
- **Screen Y**: environment-space Y axis whose magnitude is half the screen
  height in meters.
- **Environment Rotation**: physical environment orientation used to align
  stylus poses.

`Display.GetEnvironment()` returns the native environment only in `LocalAdn`
mode.

### DisplayHandle

The parent object for `Display`. It can be moved, rotated, and scaled.

- **Origin X / Origin Y** select the virtual screen origin.
- **Scale Mode** chooses real size, unit width, or unit height.
- **Show Display Border** draws the screen boundary in Scene view.
- **Show Ruler** draws a measurement ruler.

Native environment marker gizmos are available only in `LocalAdn` mode.

### StylusesCreator

Creates or removes a `Stylus` GameObject for each connected stylus. Created
styluses are available through:

```csharp
IReadOnlyList<Stylus> styluses = stylusesCreator.Styluses;
```

Assign **Stylus Go Template** to a prefab containing a `Stylus` component. The
included `StylusTemplate.prefab` can be used as a starting point.

Custom styluses can combine a Hardware Extension Module, an input button, and a
connected tracker. Give each extension node a non-empty `Tag` property. In
`LocalAdn` mode, add that value to
`DisplayStylusConnection.Stylus Tags`. Proxy-side discovery is documented in
the Proxy repository.

### Stylus

Exposes world-space tracking and button state:

- `event Action<Stylus, bool> OnUpdateButtonPhase`
- `event Action<Pose, Vector3, Vector3> OnUpdatedPose`
- `event Action<Stylus> OnDestroying`
- `Pose ExtrapolatedPose`
- `Vector3 ExtrapolatedVelocity`
- `Vector3 ExtrapolatedAngularVelocity`
- `string SourceId`

`OnUpdateButtonPhase` is called for each new state update. If a stylus
disconnects while pressed, the final callback reports the released state.

`OnUpdatedPose` runs after the world-space pose is applied and includes pose,
linear velocity, and angular velocity.

Configure prediction through `DisplayStylusConnection.ExtrapolationSeconds`.
`Stylus.ExtrapolationTime` remains only for source compatibility.

## Testing in the Unity Editor

Stylus transforms are updated from `Application.onBeforeRender`. Keep the
**Game** view open and active while testing in the Editor; Unity may not invoke
`onBeforeRender` otherwise.

For ADN and device information, see the
[official Antilatency documentation](https://developers.antilatency.com/).
