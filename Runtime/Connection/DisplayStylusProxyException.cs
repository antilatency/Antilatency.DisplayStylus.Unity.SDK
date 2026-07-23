using System;

namespace Antilatency.DisplayStylus.SDK {
    public sealed class DisplayStylusProxyException : Exception {
        public DisplayStylusProxyException(int statusCode, string code, string message) : base(message) {
            StatusCode = statusCode;
            Code = code;
        }

        public int StatusCode { get; }
        public string Code { get; }
    }
}
