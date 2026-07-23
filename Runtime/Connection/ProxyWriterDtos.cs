using System;

namespace Antilatency.DisplayStylus.SDK {
    [Serializable]
    internal sealed class ProxyAcquireLeaseRequestDto {
        public string clientId;
        public int durationSeconds;
    }

    [Serializable]
    internal sealed class ProxyRenewLeaseRequestDto {
        public string leaseId;
        public int durationSeconds;
    }

    [Serializable]
    internal sealed class ProxyReleaseLeaseRequestDto {
        public string leaseId;
    }

    [Serializable]
    internal sealed class ProxySetStringPropertyRequestDto {
        public string leaseId;
        public string value;
    }

    [Serializable]
    internal sealed class ProxyDeletePropertyRequestDto {
        public string leaseId;
    }

    [Serializable]
    internal sealed class ProxySetDisplayConfigRequestDto {
        public string leaseId;
        public uint configId;
    }

    [Serializable]
    internal sealed class ProxyWriteLeaseResponseDto {
        public bool granted;
        public ProxyWriteLeaseDto lease;
        public string reason;
    }

    [Serializable]
    internal sealed class ProxyWriteLeaseDto {
        public string leaseId;
        public string clientId;
        public string expiresAtUtc;
    }

    [Serializable]
    internal sealed class ProxyErrorResponseDto {
        public string code;
        public string message;
    }
}
