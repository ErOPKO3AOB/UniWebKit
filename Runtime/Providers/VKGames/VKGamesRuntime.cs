#if UNIWEB_VK_GAMES
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace UniWebKit
{
    // One main-thread receiver shared by VK providers, including environment updates.
    internal sealed class VKGamesRuntime : MonoBehaviour
    {
        [Serializable]
        internal sealed class Reply
        {
            public string requestId, id, name, avatar, orderId, status, error;
            public bool success;
            public int errorCode;
            internal UniWebError Error => success ? null : new UniWebError(
                Enum.IsDefined(typeof(UniWebErrorCode), errorCode) ? (UniWebErrorCode)errorCode : UniWebErrorCode.Failed, error);
        }
        private sealed class Pending { internal Action<Reply> callback; internal float deadline; }
        private static VKGamesRuntime _instance;
        private readonly Dictionary<string, Pending> _pending = new Dictionary<string, Pending>();
        private readonly List<string> _expired = new List<string>();
        internal event Action<bool> PauseChanged;
        internal event Action ProfileInvalidated;
        internal event Action EnvironmentChanged;
        internal bool IsPaused { get; private set; }
        internal static bool IsBrowser
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        internal static VKGamesRuntime Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var host = new GameObject("UniWebKit VK " + Guid.NewGuid().ToString("N"));
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<VKGamesRuntime>();
#if UNITY_WEBGL && !UNITY_EDITOR
                UniWebVK_RegisterRuntime(host.name);
#endif
                return _instance;
            }
        }

        internal static bool Supports(string method)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return UniWebVK_SupportsMethod(method) == 1;
#else
            return false;
#endif
        }

        internal static EnvirDeviceType DeviceType
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                int value = UniWebVK_GetDeviceType();
                return value == 1 ? EnvirDeviceType.Desktop : value == 2 ? EnvirDeviceType.Mobile : EnvirDeviceType.Unknown;
#else
                return EnvirDeviceType.Unknown;
#endif
            }
        }

        internal static string Language
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return LocalizationProviderBase.NormalizeLanguage(UniWebVK_GetLanguage()) ?? "en";
#else
                return LocalLocalizationProvider.SystemLanguageCode();
#endif
            }
        }

        internal static Action Request(string operation, string value, Action<Reply> callback, bool transaction = false)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var instance = Instance;
            var id = Guid.NewGuid().ToString("N");
            instance._pending.Add(id, new Pending { callback = callback,
                deadline = transaction ? float.PositiveInfinity : Time.realtimeSinceStartup + 40f });
            try { UniWebVK_Request(instance.gameObject.name, id, operation, value ?? ""); }
            catch (Exception exception) { instance.Complete(id, Failure(UniWebErrorCode.Failed, exception.Message)); }
            return () => instance._pending.Remove(id);
#else
            callback(Failure(UniWebErrorCode.Unsupported, "VK Bridge is available only in WebGL."));
            return null;
#endif
        }

        private static Reply Failure(UniWebErrorCode code, string message)
            => new Reply { success = false, errorCode = (int)code, error = message };

        private void Complete(string id, Reply reply)
        {
            if (!_pending.TryGetValue(id, out var operation)) return;
            _pending.Remove(id);
            UniWebCallbacks.Invoke(() => operation.callback(reply));
        }

        [Preserve]
        private void OnVKReply(string json)
        {
            Reply reply;
            try { reply = JsonUtility.FromJson<Reply>(json); }
            catch (Exception) { return; }
            if (reply == null || string.IsNullOrEmpty(reply.requestId)) return;
            Complete(reply.requestId, reply);
        }

        [Preserve]
        private void OnVKPause(string value)
        {
            bool paused = value == "1";
            if (paused == IsPaused) return;
            IsPaused = paused;
            UniWebCallbacks.Raise(PauseChanged, paused);
        }

        [Preserve]
        private void OnVKProfileInvalidated(string value) => UniWebCallbacks.Raise(ProfileInvalidated);

        [Preserve]
        private void OnVKEnvironmentChanged(string value) => UniWebCallbacks.Raise(EnvironmentChanged);

        private void Update()
        {
            _expired.Clear();
            foreach (var pair in _pending)
                if (Time.realtimeSinceStartup >= pair.Value.deadline) _expired.Add(pair.Key);
            foreach (var id in _expired) Complete(id, Failure(UniWebErrorCode.Timeout, "VK Bridge did not answer the request."));
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            UniWebVK_UnregisterRuntime(gameObject.name);
#endif
            if (_instance == this) _instance = null;
            var requests = new List<string>(_pending.Keys);
            foreach (var id in requests) Complete(id, Failure(UniWebErrorCode.Disposed, "VK runtime was destroyed."));
            PauseChanged = null;
            ProfileInvalidated = null;
            EnvironmentChanged = null;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void UniWebVK_RegisterRuntime(string host);
        [DllImport("__Internal")] private static extern void UniWebVK_UnregisterRuntime(string host);
        [DllImport("__Internal")] private static extern void UniWebVK_Request(string host, string id, string operation, string value);
        [DllImport("__Internal")] private static extern int UniWebVK_SupportsMethod(string method);
        [DllImport("__Internal")] private static extern int UniWebVK_GetDeviceType();
        [DllImport("__Internal")] private static extern string UniWebVK_GetLanguage();
#endif
    }
}
#endif
