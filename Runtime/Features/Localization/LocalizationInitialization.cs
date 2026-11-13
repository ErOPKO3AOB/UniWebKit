using System;
using System.Collections;
using UnityEngine;

namespace UniWebKit
{
    // Exists only while waiting for SDK startup. Some SDKs reset their events during startup.
    internal sealed class LocalizationInitialization : MonoBehaviour, IDisposable
    {
        internal static IDisposable WhenReady(Func<bool> ready, Action initialize)
        {
            if (ready()) { initialize(); return null; }
            var host = new GameObject("UniWebKit Localization Initialization");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<LocalizationInitialization>();
            runner.StartCoroutine(runner.Wait(ready, initialize));
            return runner;
        }

        private IEnumerator Wait(Func<bool> ready, Action initialize)
        {
            while (!ready()) yield return null;
            try { initialize(); }
            finally { Dispose(); }
        }

        public void Dispose()
        {
            if (this == null) return;
            StopAllCoroutines();
            Destroy(gameObject);
        }
    }
}
