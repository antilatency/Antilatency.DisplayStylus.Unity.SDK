using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    internal static class ProxySnapshotBinaryDecoder {
        public const int ProtocolVersion = 2;
        public const int MaximumSnapshotBytes = 4 * 1024 * 1024;
        private const uint Magic = 0x50534441;
        private const ushort NodesIncludedFlag = 1 << 0;
        private const int MaximumStringBytes = 1024 * 1024;
        private const int MaximumNodes = 65_536;
        private const int MaximumPropertiesPerNode = 65_536;
        private const int MaximumStyluses = 4_096;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static DisplayStylusFrame Decode(byte[] payload, int count) {
            if (payload == null) {
                throw new ArgumentNullException(nameof(payload));
            }
            if (count < 0 || count > payload.Length || count > MaximumSnapshotBytes) {
                throw new InvalidDataException($"Proxy snapshot length {count} is invalid.");
            }

            var reader = new Reader(payload, count);
            if (reader.ReadUInt32() != Magic) {
                throw new InvalidDataException("Proxy snapshot binary magic is invalid.");
            }
            var version = reader.ReadUInt16();
            if (version != ProtocolVersion) {
                throw new InvalidDataException(
                    $"Unsupported proxy protocol version {version}; expected {ProtocolVersion}.");
            }
            var flags = reader.ReadUInt16();
            if ((flags & ~NodesIncludedFlag) != 0) {
                throw new InvalidDataException("Proxy snapshot uses unsupported protocol flags.");
            }

            var sequence = reader.ReadInt64();
            reader.ReadInt64(); // UTC ticks are not needed by the Unity frame.
            reader.ReadUInt32(); // ADN update ID is not needed by the Unity frame.
            var source = reader.ReadString();

            if ((flags & NodesIncludedFlag) != 0) {
                var nodeCount = reader.ReadBoundedCount(MaximumNodes, 17, "node");
                for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++) {
                    reader.ReadUInt32();
                    reader.SkipNullableUInt32();
                    reader.SkipString();
                    reader.SkipString();
                    var propertyCount = reader.ReadBoundedCount(MaximumPropertiesPerNode, 8, "property");
                    for (var propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++) {
                        reader.SkipString();
                        reader.SkipString();
                    }
                }
            }

            DisplayStylusDisplayFrame display = null;
            if (reader.ReadBoolean()) {
                display = new DisplayStylusDisplayFrame {
                    Connected = reader.ReadBoolean()
                };
                reader.SkipNullableUInt32();
                display.ConfigId = reader.ReadUInt32();
                display.ConfigCount = reader.ReadUInt32();
                display.ScreenPosition = reader.ReadVector3();
                display.ScreenX = reader.ReadVector3();
                display.ScreenY = reader.ReadVector3();
                display.EnvironmentRotation = reader.ReadQuaternion();
            }

            var stylusCount = reader.ReadBoundedCount(MaximumStyluses, 74, "stylus");
            var styluses = new DisplayStylusDeviceFrame[stylusCount];
            for (var i = 0; i < stylusCount; i++) {
                var id = reader.ReadString();
                reader.ReadUInt32();
                reader.ReadUInt32();
                var connected = reader.ReadBoolean();
                var buttonPressed = reader.ReadBoolean();
                var position = reader.ReadVector3();
                var rotation = reader.ReadQuaternion();
                styluses[i] = new DisplayStylusDeviceFrame {
                    Id = id,
                    Connected = connected,
                    ButtonPressed = buttonPressed,
                    Pose = new Pose(position, rotation),
                    Velocity = reader.ReadVector3(),
                    LocalAngularVelocity = reader.ReadVector3(),
                    TrackingStage = reader.ReadString(),
                    Stability = reader.ReadSingle()
                };
            }

            reader.EnsureFullyConsumed();
            return new DisplayStylusFrame {
                Sequence = sequence,
                Source = string.IsNullOrEmpty(source) ? "proxy" : source,
                ReceivedAtRealtime = Time.realtimeSinceStartupAsDouble,
                Display = display,
                Styluses = styluses
            };
        }

        private struct Reader {
            private readonly byte[] _buffer;
            private readonly int _end;
            private int _position;

            public Reader(byte[] buffer, int count) {
                _buffer = buffer;
                _position = 0;
                _end = count;
            }

            public bool ReadBoolean() {
                Require(1);
                switch (_buffer[_position++]) {
                    case 0: return false;
                    case 1: return true;
                    default: throw new InvalidDataException("Proxy snapshot contains an invalid boolean.");
                }
            }

            public ushort ReadUInt16() {
                Require(2);
                var value = (ushort)(_buffer[_position] | (_buffer[_position + 1] << 8));
                _position += 2;
                return value;
            }

            public int ReadInt32() => unchecked((int)ReadUInt32());

            public uint ReadUInt32() {
                Require(4);
                var value = (uint)(_buffer[_position] |
                    (_buffer[_position + 1] << 8) |
                    (_buffer[_position + 2] << 16) |
                    (_buffer[_position + 3] << 24));
                _position += 4;
                return value;
            }

            public long ReadInt64() {
                var low = ReadUInt32();
                var high = ReadUInt32();
                return unchecked((long)(low | ((ulong)high << 32)));
            }

            public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

            public string ReadString() {
                var length = ReadStringLength();
                string result;
                try {
                    result = StrictUtf8.GetString(_buffer, _position, length);
                }
                catch (DecoderFallbackException exception) {
                    throw new InvalidDataException("Proxy snapshot contains invalid UTF-8.", exception);
                }
                _position += length;
                return result;
            }

            public void SkipString() {
                var length = ReadStringLength();
                _position += length;
            }

            public void SkipNullableUInt32() {
                if (ReadBoolean()) {
                    ReadUInt32();
                }
            }

            public int ReadBoundedCount(int maximum, int minimumBytesPerItem, string name) {
                var count = ReadInt32();
                if (count < 0 || count > maximum ||
                    (minimumBytesPerItem > 0 && count > (_end - _position) / minimumBytesPerItem)) {
                    throw new InvalidDataException($"Proxy snapshot {name} count {count} is invalid.");
                }
                return count;
            }

            public Vector3 ReadVector3() => new(ReadSingle(), ReadSingle(), ReadSingle());

            public Quaternion ReadQuaternion() =>
                new(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

            public void EnsureFullyConsumed() {
                if (_position != _end) {
                    throw new InvalidDataException($"Proxy snapshot has {_end - _position} trailing bytes.");
                }
            }

            private int ReadStringLength() {
                var length = ReadInt32();
                if (length < 0 || length > MaximumStringBytes) {
                    throw new InvalidDataException($"Proxy snapshot string length {length} is invalid.");
                }
                Require(length);
                return length;
            }

            private void Require(int count) {
                if (count < 0 || count > _end - _position) {
                    throw new EndOfStreamException("Proxy snapshot binary payload is truncated.");
                }
            }
        }
    }
}
