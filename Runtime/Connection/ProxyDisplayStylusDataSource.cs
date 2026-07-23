using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Antilatency.Alt.Environment;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    internal sealed class ProxyDisplayStylusDataSource : IDisplayStylusDataSource {
        private const int ReceiveBufferSize = 64 * 1024;

        private readonly Uri _streamUri;
        private readonly TimeSpan _reconnectDelay;
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _receiveTask;
        private byte[] _pendingPayload;
        private DisplayStylusFrame _latestFrame;
        private string _status = "Connecting to proxy";
        private int _disconnectPending;
        private long _disconnectedSequence;

        public ProxyDisplayStylusDataSource(string proxyBaseUrl, float reconnectDelaySeconds) {
            _streamUri = BuildStreamUri(proxyBaseUrl);
            _reconnectDelay = TimeSpan.FromSeconds(Mathf.Max(0.1f, reconnectDelaySeconds));
            _receiveTask = ReceiveLoop(_stop.Token);
        }

        public string Status => Volatile.Read(ref _status);
        public IEnvironment LocalEnvironment => null;

        public void Tick(float extrapolationSeconds) {
            if (Interlocked.Exchange(ref _disconnectPending, 0) != 0) {
                _latestFrame = new DisplayStylusFrame {
                    Sequence = --_disconnectedSequence,
                    Source = "proxy",
                    ReceivedAtRealtime = Time.realtimeSinceStartupAsDouble,
                    Display = new DisplayStylusDisplayFrame { Connected = false },
                    Styluses = Array.Empty<DisplayStylusDeviceFrame>()
                };
            }

            var payload = Interlocked.Exchange(ref _pendingPayload, null);
            if (payload == null) {
                return;
            }

            try {
                var frame = ProxySnapshotBinaryDecoder.Decode(payload, payload.Length);
                _latestFrame = frame;
                SetStatus(frame.Display?.Connected == true
                    ? $"Proxy connected ({_streamUri.Authority}, display ready, binary v{ProxySnapshotBinaryDecoder.ProtocolVersion})"
                    : $"Proxy connected ({_streamUri.Authority}), waiting for proxy display task");
            }
            catch (Exception exception) {
                SetStatus($"Proxy snapshot error: {exception.Message}");
            }
        }

        public bool TryGetLatestFrame(out DisplayStylusFrame frame) {
            frame = _latestFrame;
            return frame != null;
        }

        public void Dispose() {
            _stop.Cancel();
            try {
                _receiveTask.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) {
            }
            catch (Exception) {
            }
            _stop.Dispose();
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken) {
            var attempt = 0;
            while (!cancellationToken.IsCancellationRequested) {
                try {
                    using var socket = new ClientWebSocket();
                    socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                    attempt++;
                    SetStatus($"Connecting to proxy ({_streamUri.Authority}, attempt {attempt})");
                    await socket.ConnectAsync(_streamUri, cancellationToken).ConfigureAwait(false);
                    attempt = 0;
                    SetStatus($"Proxy connected ({_streamUri.Authority})");
                    await ReceiveMessages(socket, cancellationToken).ConfigureAwait(false);
                    if (!cancellationToken.IsCancellationRequested) {
                        MarkDisconnected("Proxy disconnected");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    break;
                }
                catch (Exception exception) {
                    MarkDisconnected($"Proxy disconnected: {exception.Message}");
                }

                await Task.Delay(_reconnectDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        private void MarkDisconnected(string status) {
            SetStatus($"{status}. Reconnecting in {_reconnectDelay.TotalSeconds:0.###} s");
            Interlocked.Exchange(ref _pendingPayload, null);
            Interlocked.Exchange(ref _disconnectPending, 1);
        }

        private void SetStatus(string status) {
            Volatile.Write(ref _status, status);
        }

        private async Task ReceiveMessages(ClientWebSocket socket, CancellationToken cancellationToken) {
            var buffer = new byte[ReceiveBufferSize];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested) {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                        .ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) {
                        if (socket.State == WebSocketState.CloseReceived) {
                            await socket.CloseOutputAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "unity client reconnecting",
                                cancellationToken).ConfigureAwait(false);
                        }
                        return;
                    }
                    if (result.MessageType != WebSocketMessageType.Binary) {
                        throw new InvalidDataException("The proxy sent a non-binary WebSocket message.");
                    }
                    message.Write(buffer, 0, result.Count);
                    if (message.Length > ProxySnapshotBinaryDecoder.MaximumSnapshotBytes) {
                        throw new InvalidDataException("The proxy snapshot exceeds the 4 MiB client limit.");
                    }
                } while (!result.EndOfMessage);

                Interlocked.Exchange(ref _pendingPayload, message.ToArray());
            }
        }

        private static Uri BuildStreamUri(string proxyBaseUrl) {
            if (!Uri.TryCreate(proxyBaseUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)) {
                throw new ArgumentException("Proxy Base URL must be an absolute HTTP or HTTPS URL.", nameof(proxyBaseUrl));
            }

            var builder = new UriBuilder(new Uri(baseUri, "/api/v2/stream")) {
                Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws"
            };
            return builder.Uri;
        }
    }
}
