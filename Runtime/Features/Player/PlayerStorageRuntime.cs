using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace UniWebKit
{
    // A single main-thread runner. It has no SDK event subscriptions or managed timers.
    public sealed class PlayerStorageRuntime : MonoBehaviour
    {
        private static PlayerStorageRuntime _instance;
        private sealed class Pending
        {
            public float deadline;
            public Action<PlayerStorageBackendResult> callback;
        }
        [Serializable]
        private sealed class Reply
        {
            public string requestId;
            public string data;
            public bool success;
            public bool cloudConfirmed;
            public string error;
            public int errorCode;
        }
        private readonly Dictionary<string, Pending> _pending = new Dictionary<string, Pending>();
        private readonly List<string> _expired = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _instance = null;

        private static float SafeTimeout(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 30f : Mathf.Max(1f, value);

        private static PlayerStorageRuntime Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var host = new GameObject("UniWebKit Storage " + Guid.NewGuid().ToString("N"));
                DontDestroyOnLoad(host);
                return _instance = host.AddComponent<PlayerStorageRuntime>();
            }
        }

        internal static void WhenReady(Func<bool> ready, Action action,
            Action<PlayerStorageBackendResult> onError, float timeout)
            => Instance.StartCoroutine(Instance.WaitForReady(ready, action, onError, timeout));

        private IEnumerator WaitForReady(Func<bool> ready, Action action,
            Action<PlayerStorageBackendResult> onError, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + SafeTimeout(timeout);
            while (Time.realtimeSinceStartup < deadline)
            {
                bool available;
                try { available = ready(); }
                catch (Exception exception) { onError(Failure(PlayerStorageErrorCode.Failed, exception.Message)); yield break; }
                if (available)
                {
                    try { action(); }
                    catch (Exception exception) { onError(Failure(PlayerStorageErrorCode.Failed, exception.Message)); }
                    yield break;
                }
                yield return null;
            }
            onError(Failure(PlayerStorageErrorCode.Timeout, "The platform SDK did not become ready."));
        }

        internal static void Request(string backend, string operation, string key, string profile,
            string document, string bindings, float timeout, Action<PlayerStorageBackendResult> callback)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && (UNIWEB_VK_GAMES || UNIWEB_GAMEPUSH)
            var instance = Instance;
            string requestId = Guid.NewGuid().ToString("N");
            instance._pending.Add(requestId, new Pending
            { deadline = Time.realtimeSinceStartup + SafeTimeout(timeout), callback = callback });
            try { UniWebStorage_Request(instance.gameObject.name, requestId, backend, operation, key, profile, document ?? "", bindings ?? ""); }
            catch (Exception exception)
            {
                instance._pending.Remove(requestId);
                callback(Failure(PlayerStorageErrorCode.Failed, exception.Message));
            }
#else
            callback(Failure(PlayerStorageErrorCode.Unsupported, "The browser storage bridge is available only in WebGL."));
#endif
        }

        internal static string VkProfileId
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && UNIWEB_VK_GAMES
                return UniWebStorage_VkProfile();
#else
                return "guest";
#endif
            }
        }
        internal static bool VkAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR && UNIWEB_VK_GAMES
                return UniWebStorage_VkAvailable() != 0;
#else
                return false;
#endif
            }
        }

        private void Update()
        {
            _expired.Clear();
            foreach (var pair in _pending)
                if (Time.realtimeSinceStartup >= pair.Value.deadline) _expired.Add(pair.Key);
            foreach (var id in _expired)
            {
                if (!_pending.TryGetValue(id, out var pending)) continue;
                _pending.Remove(id);
                pending.callback(Failure(PlayerStorageErrorCode.Timeout, "The platform did not confirm the storage request."));
            }
        }

        [Preserve]
        private void OnStorageReply(string json)
        {
            Reply reply;
            try { reply = JsonUtility.FromJson<Reply>(json); }
            catch (Exception) { return; }
            if (reply == null || string.IsNullOrEmpty(reply.requestId) || !_pending.TryGetValue(reply.requestId, out var pending)) return;
            _pending.Remove(reply.requestId);
            var error = reply.success ? null : new PlayerStorageError(
                Enum.IsDefined(typeof(PlayerStorageErrorCode), reply.errorCode)
                    && reply.errorCode != 0 ? (PlayerStorageErrorCode)reply.errorCode : PlayerStorageErrorCode.Failed, reply.error);
            pending.callback(new PlayerStorageBackendResult(reply.data, reply.cloudConfirmed, error));
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            var requests = new List<Pending>(_pending.Values); _pending.Clear();
            foreach (var pending in requests)
                pending.callback(Failure(PlayerStorageErrorCode.Disposed, "The storage runtime was destroyed."));
        }

        internal static PlayerStorageBackendResult Failure(PlayerStorageErrorCode code, string message)
            => new PlayerStorageBackendResult(error: new PlayerStorageError(code, message));

#if UNITY_WEBGL && !UNITY_EDITOR && (UNIWEB_VK_GAMES || UNIWEB_GAMEPUSH)
        [DllImport("__Internal")]
        private static extern void UniWebStorage_Request(string host, string id, string backend, string operation,
            string key, string profile, string document, string bindings);
#endif
#if UNITY_WEBGL && !UNITY_EDITOR && UNIWEB_VK_GAMES
        [DllImport("__Internal")] private static extern string UniWebStorage_VkProfile();
        [DllImport("__Internal")] private static extern int UniWebStorage_VkAvailable();
#endif
    }
}
