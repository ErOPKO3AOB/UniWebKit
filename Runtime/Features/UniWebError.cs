using System;
using UnityEngine;

namespace UniWebKit
{
    public enum UniWebErrorCode { Unsupported, NotReady, Busy, InvalidArgument, InvalidData, Failed, Timeout, Disposed }

    public sealed class UniWebError
    {
        public UniWebErrorCode Code { get; }
        public string Message { get; }
        public UniWebError(UniWebErrorCode code, string message) { Code = code; Message = message ?? string.Empty; }
        public override string ToString() => $"{Code}: {Message}";
    }

    internal static class UniWebCallbacks
    {
        internal static void Invoke(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        internal static void Raise<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList()) Invoke(() => handler(value));
        }
        internal static void Raise(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList()) Invoke(handler);
        }
    }
}
