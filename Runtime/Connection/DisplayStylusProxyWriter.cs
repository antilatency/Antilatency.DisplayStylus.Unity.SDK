using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Antilatency.DisplayStylus.SDK {
    /// <summary>
    /// Owns one exclusive proxy write lease and sends low-frequency control commands.
    /// Snapshot streaming remains on the separate binary WebSocket data plane.
    /// </summary>
    public sealed class DisplayStylusProxyWriter : IDisposable {
        public const int DefaultLeaseDurationSeconds = 15;
        public const int MaximumLeaseDurationSeconds = 120;
        private static readonly HttpStatusCode LockedStatusCode = (HttpStatusCode)423;

        private readonly HttpClient _http;

        public DisplayStylusProxyWriter(string proxyBaseUrl, string clientId) {
            if (string.IsNullOrWhiteSpace(clientId)) {
                throw new ArgumentException("A non-empty proxy writer client ID is required.", nameof(clientId));
            }

            _http = new HttpClient {
                BaseAddress = ValidateBaseAddress(proxyBaseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };
            ClientId = clientId;
        }

        public string ClientId { get; }
        public string LeaseId { get; private set; }
        public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
        public string LastLeaseFailure { get; private set; }
        public bool HasLease => !string.IsNullOrEmpty(LeaseId);

        /// <summary>
        /// Attempts to acquire the exclusive write channel. Returns false when another client owns it.
        /// </summary>
        public async Task<bool> AcquireAsync(
            int durationSeconds = DefaultLeaseDurationSeconds,
            CancellationToken cancellationToken = default) {
            if (HasLease) {
                throw new InvalidOperationException("This writer already owns a proxy write lease.");
            }
            ValidateDuration(durationSeconds);

            var response = await SendJsonAsync(
                HttpMethod.Post,
                "api/v1/lease/acquire",
                new ProxyAcquireLeaseRequestDto {
                    clientId = ClientId,
                    durationSeconds = durationSeconds
                },
                cancellationToken,
                LockedStatusCode);
            var leaseResponse = ParseLeaseResponse(response.Body);
            if (response.StatusCode == LockedStatusCode || !leaseResponse.granted) {
                LastLeaseFailure = leaseResponse.reason ?? "The proxy write channel is occupied.";
                return false;
            }

            ApplyLease(leaseResponse);
            return true;
        }

        /// <summary>Renews the current lease. Returns false if it expired or was otherwise lost.</summary>
        public async Task<bool> RenewAsync(
            int durationSeconds = DefaultLeaseDurationSeconds,
            CancellationToken cancellationToken = default) {
            var leaseId = RequireLease();
            ValidateDuration(durationSeconds);

            var response = await SendJsonAsync(
                HttpMethod.Post,
                "api/v1/lease/renew",
                new ProxyRenewLeaseRequestDto {
                    leaseId = leaseId,
                    durationSeconds = durationSeconds
                },
                cancellationToken,
                HttpStatusCode.Conflict);
            var leaseResponse = ParseLeaseResponse(response.Body);
            if (response.StatusCode == HttpStatusCode.Conflict || !leaseResponse.granted) {
                ClearLease();
                LastLeaseFailure = leaseResponse.reason ?? "The proxy write lease was lost.";
                return false;
            }

            ApplyLease(leaseResponse);
            return true;
        }

        /// <summary>Releases the write channel. It is safe to call when no lease is held.</summary>
        public async Task ReleaseAsync(CancellationToken cancellationToken = default) {
            if (!HasLease) {
                return;
            }

            var leaseId = LeaseId;
            try {
                await SendJsonAsync(
                    HttpMethod.Post,
                    "api/v1/lease/release",
                    new ProxyReleaseLeaseRequestDto { leaseId = leaseId },
                    cancellationToken,
                    HttpStatusCode.Conflict);
            }
            finally {
                ClearLease();
            }
        }

        public async Task SetStringPropertyAsync(
            uint nodeId,
            string key,
            string value,
            CancellationToken cancellationToken = default) {
            ValidatePropertyKey(key);
            if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            await SendWriteAsync(
                HttpMethod.Put,
                $"api/v1/nodes/{nodeId}/properties/{Uri.EscapeDataString(key)}",
                new ProxySetStringPropertyRequestDto {
                    leaseId = RequireLease(),
                    value = value
                },
                cancellationToken);
        }

        public async Task DeletePropertyAsync(
            uint nodeId,
            string key,
            CancellationToken cancellationToken = default) {
            ValidatePropertyKey(key);
            await SendWriteAsync(
                HttpMethod.Delete,
                $"api/v1/nodes/{nodeId}/properties/{Uri.EscapeDataString(key)}",
                new ProxyDeletePropertyRequestDto { leaseId = RequireLease() },
                cancellationToken);
        }

        public async Task SetDisplayConfigAsync(
            uint configId,
            CancellationToken cancellationToken = default) {
            await SendWriteAsync(
                HttpMethod.Put,
                "api/v1/display/config",
                new ProxySetDisplayConfigRequestDto {
                    leaseId = RequireLease(),
                    configId = configId
                },
                cancellationToken);
        }

        private async Task SendWriteAsync(
            HttpMethod method,
            string path,
            object body,
            CancellationToken cancellationToken) {
            try {
                await SendJsonAsync(method, path, body, cancellationToken);
            }
            catch (DisplayStylusProxyException exception) when (
                string.Equals(exception.Code, "write_lease_required", StringComparison.Ordinal)) {
                ClearLease();
                LastLeaseFailure = exception.Message;
                throw;
            }
        }

        /// <summary>
        /// Disposes HTTP resources. Call and await ReleaseAsync first; otherwise the server retains
        /// the lease until its short TTL expires.
        /// </summary>
        public void Dispose() {
            _http.Dispose();
        }

        private async Task<ProxyHttpResponse> SendJsonAsync(
            HttpMethod method,
            string path,
            object body,
            CancellationToken cancellationToken,
            HttpStatusCode? allowedErrorStatus = null) {
            using var request = new HttpRequestMessage(method, path) {
                Content = new StringContent(JsonUtility.ToJson(body), Encoding.UTF8, "application/json")
            };
            using var response = await _http.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode && response.StatusCode != allowedErrorStatus) {
                throw CreateApiException(response.StatusCode, responseBody);
            }
            return new ProxyHttpResponse(response.StatusCode, responseBody);
        }

        private void ApplyLease(ProxyWriteLeaseResponseDto response) {
            if (response.lease == null || string.IsNullOrWhiteSpace(response.lease.leaseId)) {
                throw new InvalidOperationException("The proxy granted a lease without a lease ID.");
            }

            LeaseId = response.lease.leaseId;
            LeaseExpiresAtUtc = DateTimeOffset.TryParse(
                response.lease.expiresAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAtUtc) ? expiresAtUtc : null;
            LastLeaseFailure = null;
        }

        private void ClearLease() {
            LeaseId = null;
            LeaseExpiresAtUtc = null;
        }

        private string RequireLease() => HasLease
            ? LeaseId
            : throw new InvalidOperationException("Acquire the proxy write lease before sending a write command.");

        private static ProxyWriteLeaseResponseDto ParseLeaseResponse(string json) =>
            JsonUtility.FromJson<ProxyWriteLeaseResponseDto>(json) ??
            throw new InvalidOperationException("The proxy returned an invalid write-lease response.");

        private static DisplayStylusProxyException CreateApiException(HttpStatusCode statusCode, string body) {
            ProxyErrorResponseDto error = null;
            try {
                error = JsonUtility.FromJson<ProxyErrorResponseDto>(body);
            }
            catch (ArgumentException) {
            }

            var code = string.IsNullOrWhiteSpace(error?.code) ? "http_error" : error.code;
            var message = string.IsNullOrWhiteSpace(error?.message)
                ? $"Proxy returned HTTP {(int)statusCode} {statusCode}."
                : error.message;
            return new DisplayStylusProxyException((int)statusCode, code, message);
        }

        private static Uri ValidateBaseAddress(string proxyBaseUrl) {
            if (!Uri.TryCreate(proxyBaseUrl, UriKind.Absolute, out var result) ||
                (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps)) {
                throw new ArgumentException(
                    "Proxy Base URL must be an absolute HTTP or HTTPS URL.",
                    nameof(proxyBaseUrl));
            }
            return result.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
                ? result
                : new Uri(result.AbsoluteUri + "/");
        }

        private static void ValidateDuration(int durationSeconds) {
            if (durationSeconds < 1 || durationSeconds > MaximumLeaseDurationSeconds) {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    $"Lease duration must be between 1 and {MaximumLeaseDurationSeconds} seconds.");
            }
        }

        private static void ValidatePropertyKey(string key) {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128) {
                throw new ArgumentException("Property key must contain 1..128 characters.", nameof(key));
            }
        }

        private readonly struct ProxyHttpResponse {
            public ProxyHttpResponse(HttpStatusCode statusCode, string body) {
                StatusCode = statusCode;
                Body = body;
            }

            public HttpStatusCode StatusCode { get; }
            public string Body { get; }
        }
    }

}
