using System;
using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// Something the host can walk up to and use with E (phone, door, activity spot, guest...).
    /// Added at runtime by the game; the prompt and availability change with the game phase.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [Tooltip("How close the host has to be.")]
        public float radius = 1.3f;
        [Tooltip("Where the prompt floats above (defaults to 1.6 m above this object).")]
        public Transform promptAnchor;

        /// <summary>Text shown above it, e.g. "Talk to Guest 1". Null or empty = can't be used right now.</summary>
        public Func<string> prompt;
        /// <summary>False shows the prompt greyed out (e.g. "Over budget") and E does nothing.</summary>
        public Func<bool> canUse = () => true;
        public Action use;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public string Prompt => prompt?.Invoke();
        public bool CanUse => canUse == null || canUse();

        public Vector3 PromptPosition =>
            promptAnchor != null ? promptAnchor.position : transform.position + Vector3.up * 1.6f;

        /// <summary>Closest usable-looking interactable within reach of a point, or null.</summary>
        public static Interactable Nearest(Vector3 from)
        {
            Interactable best = null;
            float bestDist = float.MaxValue;
            foreach (var it in All)
            {
                if (string.IsNullOrEmpty(it.Prompt)) continue;
                var d = it.transform.position - from;
                d.y = 0;
                float dist = d.magnitude;
                if (dist <= it.radius && dist < bestDist)
                {
                    best = it;
                    bestDist = dist;
                }
            }
            return best;
        }
    }
}
