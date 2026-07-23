# Antilatency Display Stylus Unity SDK

Unity integration for Antilatency display styluses with a single switchable connection gateway:

- **Local ADN** creates the Antilatency Device Network and runs all device cotasks inside Unity.
- **Proxy** receives display and stylus telemetry from the standalone `Antilatency.DisplayStylus.Proxy` process.

The package supports Unity 2021.3.5f1 and later and depends on Antilatency SDK 4.6.0.

## Setup

### Add Antilatency SDK

Install the Antilatency SDK 4.6.0 package. The following subset includes the modules used by this package, including `AltEnvironmentSides`:

```text
https://github.com/AntilatencySDK/Release_4.6.0.git#subset-22a756f0fb8b2c7a4dbd47324fdc939eb60f8372
```

### Add this package

In Unity Package Manager, choose **Add package from disk** and select this package's `package.json`.

### Add samples

Open the package in Unity Package Manager and import the **Cubes** sample to see stylus interaction, pointer, and grabbing examples.

## Adding Display Stylus to a scene

In the Unity top menu, select **Display Stylus > Create In Scene**. The command creates:

- `DisplayHandle`
- `DisplayStylusConnection`
- `Display`
- `StylusesCreator`

Choose `LocalAdn` or `Proxy` on the same `DisplayStylusConnection` component. In `LocalAdn` mode the connection creates `Antilatency.SDK.DeviceNetwork` when it starts; Proxy mode does not create a local network. The mode can also be changed at runtime.

Existing scenes created with package 1.2.x remain compatible. If a scene contains `Display` and `Antilatency.SDK.DeviceNetwork` but no `DisplayStylusConnection`, the gateway is added automatically in `LocalAdn` mode at runtime.

## Using Proxy mode

Proxy mode moves ADN ownership and device cotasks into the standalone process. Unity receives display and stylus frames over WebSocket.

```text
Antilatency devices -> Proxy process -> binary WebSocket -> one or more Unity readers
                                      <- leased HTTP commands <- one lease holder
```

Multiple clients can read at the same time. Write commands use one exclusive, short-lived lease. Reading is never blocked by a writer.

### Requirements and ownership

- Run the proxy on Windows x64.
- Do not run another ADN owner, including Unity in `LocalAdn` mode, while the proxy owns ADN.
- Keep TCP port `48192` available. The local endpoint is always `http://127.0.0.1:48192`.
- Proxy mode supports desktop Unity players and the Unity Editor. WebGL cannot use the required `ClientWebSocket` API.

### 1. Start the proxy process

Download `Antilatency.DisplayStylus.Proxy-v<version>-win-x64.zip` from the
Antilatency Display Stylus Proxy GitHub Releases page, extract the complete
archive, and run the following command from its directory:

```powershell
.\run-proxy.cmd
```

The proxy always uses the real Antilatency Device Network. `/health` reports a fault if ADN cannot be opened.

Confirm that the server is healthy and is using the expected driver:

```powershell
Invoke-RestMethod http://127.0.0.1:48192/health
```

The response should have `status` set to `ok` and `driver` set to `adn-4.6.0`. If a proxy is already running on the fixed port, a second launch reports the existing instance and exits normally.

### 2. Configure the Unity scene

Select **Display Stylus > Create In Scene** once, then configure the created `DisplayStylusConnection`:

- Set **Mode** to `Proxy`.
- Keep **Proxy Base URL** at `http://127.0.0.1:48192` for a local proxy.
- Keep **Manage Local Device Network Activation** enabled. The connection creates `Antilatency.SDK.DeviceNetwork` only while `LocalAdn` owns ADN and removes it while `Proxy` owns ADN.
- Adjust **Proxy Reconnect Delay Seconds** only if the default one-second retry delay is unsuitable.
- Use **Extrapolation Seconds** to tune pose prediction for the display latency.

The connection opens `ws://127.0.0.1:48192/api/v2/stream`. It reconnects automatically when the process starts later or restarts. Useful runtime state is available through:

```csharp
Debug.Log(connection.ConnectionStatus);

if (connection.IsReady) {
    Debug.Log("The proxy and physical display are ready.");
}
```

`IsReady` becomes true after a frame reports a connected physical display. A connected proxy with no display remains not ready.

`ConnectionStatus` distinguishes reconnecting, connected-but-waiting-for-display, and display-ready states. A disconnect publishes a frame with `Display.Connected == false` and an empty stylus list so consumers can clear stale state.

### 3. Read display and stylus data

Normal gameplay code should continue using the package components:

- `Display` updates the physical screen position, axes, size, and environment rotation.
- `StylusesCreator` creates one `Stylus` GameObject for each connected stylus.
- `Stylus.OnUpdatedPose` provides the world-space pose and velocities.
- `Stylus.OnUpdateButtonPhase` provides the button state.

These components work with both `LocalAdn` and `Proxy`, so application code does not need separate tracking implementations.

For lower-level access, subscribe to `DisplayStylusConnection.FrameUpdated` or read `LatestFrame`:

```csharp
using Antilatency.DisplayStylus.SDK;
using UnityEngine;

public sealed class ProxyFrameReader : MonoBehaviour {
    [SerializeField] private DisplayStylusConnection connection;

    private void OnEnable() {
        connection.FrameUpdated += OnFrameUpdated;
    }

    private void OnDisable() {
        connection.FrameUpdated -= OnFrameUpdated;
    }

    private static void OnFrameUpdated(DisplayStylusFrame frame) {
        if (frame.Display?.Connected != true) {
            return;
        }

        Debug.Log(
            $"Frame {frame.Sequence}: config " +
            $"{frame.Display.ConfigId}/{frame.Display.ConfigCount}");

        foreach (var stylus in frame.Styluses) {
            Debug.Log($"{stylus.Id}: {stylus.Pose.position}, pressed={stylus.ButtonPressed}");
        }
    }
}
```

`FrameUpdated` is raised on the Unity main thread. High-frequency snapshots use a bounded binary protocol; vectors, quaternions, poses, and velocities are not encoded as JSON. When a reader is slow, stale snapshots are replaced by the newest one instead of building an unbounded backlog. Do not poll an HTTP endpoint every Unity frame.

Native interfaces such as `INetwork`, `ICotask`, and `IEnvironment` cannot cross the process boundary. Consequently, `Display.GetEnvironment()` and `DisplayStylusConnection.LocalEnvironment` return `null` in Proxy mode.

### 4. Write through the proxy

Writing is a separate, low-frequency HTTP control path. The required lifecycle is:

```text
Acquire lease -> Write one or more changes -> Release lease
```

Create a writer from a connection already configured in `Proxy` mode. Give each tool or subsystem a stable diagnostic client ID. Always release the lease in `finally`:

```csharp
using System.Threading.Tasks;
using Antilatency.DisplayStylus.SDK;
using UnityEngine;

public sealed class ProxyConfigurationWriter : MonoBehaviour {
    [SerializeField] private DisplayStylusConnection connection;

    public async Task ApplyChanges(uint configId, uint idleNodeId) {
        using (var writer = connection.CreateProxyWriter("unity-configuration-panel")) {
            if (!await writer.AcquireAsync(15)) {
                Debug.LogWarning($"Proxy write line is busy: {writer.LastLeaseFailure}");
                return;
            }

            try {
                await writer.SetDisplayConfigAsync(configId);
                await writer.SetStringPropertyAsync(idleNodeId, "Tag", "Stylus");
                await writer.DeletePropertyAsync(idleNodeId, "OldProperty");
            }
            catch (DisplayStylusProxyException exception) {
                Debug.LogError(
                    $"Proxy write failed: HTTP {exception.StatusCode}, " +
                    $"{exception.Code}: {exception.Message}");
                throw;
            }
            finally {
                await writer.ReleaseAsync();
            }
        }
    }
}
```

Only one lease can exist at a time:

- `AcquireAsync()` returns `true` and populates `LeaseId` and `LeaseExpiresAtUtc` when ownership is granted.
- If another client owns the line, the server returns `423 Locked`; `AcquireAsync()` returns `false` and puts the explanation in `LastLeaseFailure`.
- The default lease duration is 15 seconds and the maximum is 120 seconds.
- For a longer edit session, call `RenewAsync()` before `LeaseExpiresAtUtc`. It returns `false` and clears the local lease if ownership was lost.
- `ReleaseAsync()` frees the line and is safe when no lease is held.
- Call `ReleaseAsync()` before `Dispose()`; a crashed client retains the lease only until its TTL expires.

### Supported write commands

| Method | Effect | Restrictions |
| --- | --- | --- |
| `SetDisplayConfigAsync(configId)` | Selects an existing physical-display configuration. | The ID must be less than `LatestFrame.Display.ConfigCount`. This does not create or edit a calibration. |
| `SetStringPropertyAsync(nodeId, key, value)` | Sets an ADN string property. | The target node must be idle. |
| `DeletePropertyAsync(nodeId, key)` | Deletes an ADN property. | The target node must be idle. |

Changing the active display configuration is supported while the display cotask is running. The proxy selects the new configuration, recreates the tracking environment, and restarts its stylus cotasks. A short tracking interruption is therefore expected.

ADN property tasks can start only on idle nodes. Display and stylus nodes managed by the proxy normally have active cotasks, so property writes to those nodes usually return `409 device_busy`. Configure persistent tags before starting the proxy, or use a node that is known to be idle. The Unity frame API exposes display and stylus telemetry rather than the complete ADN node tree; obtain maintenance node IDs with an ADN administration tool.

### Handle write errors

A successful command completes with no return value (`HTTP 204 No Content`). For an expected API failure, `DisplayStylusProxyWriter` throws `DisplayStylusProxyException` with:

- `StatusCode`: the numeric HTTP response status;
- `Code`: a stable machine-readable code;
- `Message`: the human-readable message returned by the server.

```csharp
try {
    await writer.DeletePropertyAsync(nodeId, "OldProperty");
}
catch (DisplayStylusProxyException exception) {
    Debug.LogError(
        $"Delete failed: HTTP {exception.StatusCode}, " +
        $"{exception.Code}: {exception.Message}");
}
```

| HTTP status | Code | Meaning |
| --- | --- | --- |
| `400 Bad Request` | `invalid_command` | A property key or display configuration ID is invalid. |
| `404 Not Found` | `node_not_found` | The requested ADN node does not exist. |
| `409 Conflict` | `device_busy` | The node is running a cotask, or the physical display is unavailable. |
| `409 Conflict` | `write_lease_required` | The lease is missing, expired, released, or no longer owned by this writer. |

When `write_lease_required` is received, the writer clears its local lease and copies the server message to `LastLeaseFailure`. Calling a write method before acquiring a lease throws `InvalidOperationException` locally and sends no HTTP request. Invalid property keys throw `ArgumentException` locally. Connection failures and cancellations retain their standard .NET exception types.

Lease and write messages use JSON because they are small and infrequent. Only the high-frequency telemetry path requires the binary serializer.

### Switch modes at runtime

Changing `Mode` disposes the current source and starts the selected one:

```csharp
connection.ProxyBaseUrl = "http://127.0.0.1:48192";
connection.Mode = DisplayStylusConnectionMode.Proxy;
```

Call `Reconnect()` after changing the URL of an already active Proxy source. Stop the standalone proxy before switching back:

```csharp
connection.Mode = DisplayStylusConnectionMode.LocalAdn;
```

### Troubleshooting

| Symptom | Check |
| --- | --- |
| `ConnectionStatus` remains `Connecting to proxy` | Confirm that the process is running, `/health` responds, the URL is correct, and port `48192` is not blocked. |
| The proxy cannot open ADN | Stop other ADN owners, including another proxy or Unity in `LocalAdn` mode. |
| `IsReady` is false although the socket is connected | Check that the physical display node is connected and that its cotask can start. |
| Status says `waiting for proxy display task` | Stop Unity objects or tools that still own local ADN/PCE tasks. Keep **Manage Local Device Network Activation** enabled and restart Play Mode. |
| Unity was open while upgrading the Antilatency SDK package | Restart the Unity Editor before connecting or disconnecting USB devices. Unity cannot unload the old native plugin during a hot package upgrade; keeping two SDK versions loaded in one Editor process can crash native device callbacks. |
| No stylus is created | Check the proxy `HardwareNameContains` and `StylusTags` settings and the device `Tag` property. |
| `AcquireAsync()` returns false | Another client owns the write lease. Read `LastLeaseFailure` or wait for the reported lease to expire. |
| A property write returns `device_busy` | The target node has an active cotask and cannot run an ADN property task. |
| `Proxy snapshot error` mentions a protocol version | Use matching versions of the proxy process and this Unity package. |

The current server has no TLS or authentication and binds to loopback by default. Do not expose it to another machine or an untrusted network until transport security and client authentication are added.

## Components

### DisplayStylusConnection

Selects local ADN or the proxy and exposes:

- `Mode`
- `LatestFrame`
- `FrameUpdated`
- `ConnectionStatus`
- `IsReady`
- `ExtrapolationSeconds`
- `CreateProxyWriter(clientId)`

### Display

The `Display` component connects virtual content to the controller managing the physical display markers.

**Sync With Physical Display Rotation** applies the physical display tilt to the virtual screen when its markers are visible to the stylus tracking device. In Editor mode outside Play Mode, this behaves as disabled.

For Editor layout work, screen parameters can be set manually. In Play Mode they update from the selected connection source:

- **Screen Position**: screen position relative to the Antilatency environment, in meters.
- **Screen X**: environment-space X axis whose magnitude is half the screen width in meters.
- **Screen Y**: environment-space Y axis whose magnitude is half the screen height in meters.
- **Environment Rotation**: physical environment orientation used to align stylus poses.

### Display Handle

`DisplayHandle` is the parent object for `Display`. It can be moved, rotated, and scaled.

- **Origin X / Origin Y** select the virtual screen origin.
- **Scale Mode** chooses real size, unit width, or unit height.
- **Show Display Border** draws the screen boundary in Scene view.
- **Show Ruler** draws a measurement ruler.

Native environment marker gizmos are available only in `LocalAdn` mode.

### StylusesCreator

`StylusesCreator` scans the current frames and creates or removes a `Stylus` GameObject for every connected stylus. Created styluses are available through:

```csharp
IReadOnlyList<Stylus> styluses = stylusesCreator.Styluses;
```

Assign **Stylus Go Template** to a prefab containing a `Stylus` component. The included `StylusTemplate.prefab` can be used as a starting point.

Multiple custom styluses can be assembled from a Hardware Extension Module, an
input button, and a connected tracker. Give each extension node a non-empty
`Tag` property. In `LocalAdn` mode, add that value to
`DisplayStylusConnection.Stylus Tags`. In `Proxy` mode, add it to
`Adn:StylusTags` in the proxy's `appsettings.json`. Hardware-name matching uses
**Hardware Name Contains** locally or `Adn:HardwareNameContains` in the proxy.

### Stylus

`Stylus` exposes world-space tracking and button state:

- `event Action<Stylus, bool> OnUpdateButtonPhase`
- `event Action<Pose, Vector3, Vector3> OnUpdatedPose`
- `event Action<Stylus> OnDestroying`
- `Pose ExtrapolatedPose`
- `Vector3 ExtrapolatedVelocity`
- `Vector3 ExtrapolatedAngularVelocity`
- `string SourceId`

`OnUpdateButtonPhase` is called for each new state update. If a stylus disconnects while pressed, the final callback reports the released state.

`OnUpdatedPose` runs after the world-space pose is applied and includes pose, linear velocity, and angular velocity.

The exact display latency depends on the machine and display. Configure prediction through `DisplayStylusConnection.ExtrapolationSeconds`. `Stylus.ExtrapolationTime` remains only for source compatibility.

### Testing in the Unity Editor

Stylus transforms are updated from `Application.onBeforeRender`. Keep the **Game** view open and active while testing in the Editor; Unity may not invoke `onBeforeRender` otherwise.

For more detailed ADN and device information, see the [official Antilatency documentation](https://developers.antilatency.com/).
