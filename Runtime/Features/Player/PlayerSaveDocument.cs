using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniWebKit
{
    [Serializable]
    internal sealed class PlayerSaveDocument
    {
        public int format = 1;
        public string owner;
        public string revision;
        public bool pending;
        public List<Entry> entries;

        [Serializable]
        internal sealed class Entry { public string key; public PlayerValue value; }

        internal static bool TryRead(string json, string owner, out PlayerSaveDocument document, bool platformOwnsIdentity = false)
        {
            document = null;
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                var parsed = JsonUtility.FromJson<PlayerSaveDocument>(json);
                if (parsed == null || parsed.format != 1 || parsed.entries == null
                    || !json.Contains("\"entries\"") || string.IsNullOrEmpty(parsed.revision) || !platformOwnsIdentity && parsed.owner != owner) return false;
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in parsed.entries)
                    if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null
                        || entry.value.data == null || !Enum.IsDefined(typeof(PlayerValueType), entry.value.type)
                        || !keys.Add(entry.key)) return false;
                if (platformOwnsIdentity) parsed.owner = owner;
                document = parsed; return true;
            }
            catch (Exception) { return false; }
        }
    }
}
