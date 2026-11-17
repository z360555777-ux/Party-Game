using System;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// The five stat axes from the design doc. Every value runs from -2 to +2.
    /// Directions match the team's "Game Data" spreadsheet: the positive pole is the spreadsheet column
    /// (Playful, Activity-focused, Big gathering, Informal, Social Butterfly).
    /// </summary>
    public enum PartyAxis
    {
        Tone,       // Serious (-) <> Playful (+)
        Structure,  // Flexible (-) <> Activity-focused (+)
        Crowd,      // Prefers small (-) <> Prefers big (+)
        Formality,  // Formal (-) <> Informal (+)
        Social      // Wallflower (-) <> Social butterfly (+)
    }

    /// <summary>Tags layered on top of the axes (the doc's "Guest Type" table).</summary>
    public enum GuestTrait
    {
        Inquisitive,
        Storyteller,
        Gossip,
        Helper
    }

    public static class Axes
    {
        public const float Min = -2f;
        public const float Max = 2f;

        public static readonly PartyAxis[] All = (PartyAxis[])Enum.GetValues(typeof(PartyAxis));
        public static int Count => All.Length;

        public static string NegativePole(PartyAxis axis) => axis switch
        {
            PartyAxis.Tone => "Serious",
            PartyAxis.Structure => "Flexible",
            PartyAxis.Crowd => "Small party",
            PartyAxis.Formality => "Formal",
            PartyAxis.Social => "Wallflower",
            _ => axis.ToString()
        };

        public static string PositivePole(PartyAxis axis) => axis switch
        {
            PartyAxis.Tone => "Playful",
            PartyAxis.Structure => "Activity-focused",
            PartyAxis.Crowd => "Big party",
            PartyAxis.Formality => "Informal",
            PartyAxis.Social => "Social butterfly",
            _ => axis.ToString()
        };

        /// <summary>Short id of the pole a value leans to, used in dialogue keys ("Playful", "Serious"...).</summary>
        public static string PoleKey(PartyAxis axis, float value) =>
            (value < 0 ? NegativePole(axis) : PositivePole(axis)).Replace(" ", "");

        /// <summary>Human-readable description of a value, e.g. "Very Playful" or "Neutral".</summary>
        public static string Describe(PartyAxis axis, float value)
        {
            if (Mathf.Abs(value) < 0.5f) return "No strong feeling";
            string pole = value < 0 ? NegativePole(axis) : PositivePole(axis);
            return Mathf.Abs(value) >= 1.5f ? "Very " + pole : pole;
        }

        public static float Clamp(float v) => Mathf.Clamp(v, Min, Max);
    }

    /// <summary>One value per axis. Serializable so it shows up in the Inspector.</summary>
    [Serializable]
    public class AxisValues
    {
        [Range(-2, 2)] public float tone;
        [Range(-2, 2)] public float structure;
        [Range(-2, 2)] public float crowd;
        [Range(-2, 2)] public float formality;
        [Range(-2, 2)] public float social;

        public AxisValues() { }

        public AxisValues(float tone, float structure, float crowd, float formality, float social)
        {
            this.tone = tone;
            this.structure = structure;
            this.crowd = crowd;
            this.formality = formality;
            this.social = social;
        }

        public float this[PartyAxis axis]
        {
            get => axis switch
            {
                PartyAxis.Tone => tone,
                PartyAxis.Structure => structure,
                PartyAxis.Crowd => crowd,
                PartyAxis.Formality => formality,
                PartyAxis.Social => social,
                _ => 0f
            };
            set
            {
                switch (axis)
                {
                    case PartyAxis.Tone: tone = value; break;
                    case PartyAxis.Structure: structure = value; break;
                    case PartyAxis.Crowd: crowd = value; break;
                    case PartyAxis.Formality: formality = value; break;
                    case PartyAxis.Social: social = value; break;
                }
            }
        }

        public AxisValues Clone() => new AxisValues(tone, structure, crowd, formality, social);
    }
}
