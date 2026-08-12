using System;
using UnityEngine;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// Kill type classification matching gd656killicon's KillType.
    /// </summary>
    public enum KillType
    {
        Normal = 0,
        Headshot = 1,
        Explosion = 2,
        Crit = 3
    }

    /// <summary>
    /// Represents a data structure for defining Kill Icon preset attributes 
    /// (corresponds to gd656killicon's ElementPreset and default configs).
    /// </summary>
    [Serializable]
    public class KillIconPreset
    {
        // =======================
        // General Properties
        // =======================
        public bool Visible { get; set; } = true;
        public string DisplayName { get; set; } = "Default";

        // =======================
        // Transform (Coordinates & Size)
        // =======================
        public float Scale { get; set; } = 1.0f;
        public float XOffset { get; set; } = 0f;
        public float YOffset { get; set; } = 100f;

        // =======================
        // Animation Parameters
        // =======================
        public float DisplayDuration { get; set; } = 3.25f;
        public float AnimationDuration { get; set; } = 0.3f;
        public float FadeOutDuration { get; set; } = 0.1f;
        public float PositionAnimationDuration { get; set; } = 0.3f;
        public float StartScale { get; set; } = 2.0f;
        public bool EnableScaleAnimation { get; set; } = true;

        // =======================
        // Layout Parameters
        // =======================
        public float IconSpacing { get; set; } = 4f;
        public int MaxVisibleIcons { get; set; } = 7;
        public int DisplayIntervalMs { get; set; } = 100;

        // =======================
        // Skull / Icon Size
        // =======================
        public float SkullSize { get; set; } = 64f;
        public float SkullSpacing { get; set; } = 60f;
        public float SkullDisplayDuration { get; set; } = 2f;
        public float SkullFadeDuration { get; set; } = 0.3f;
        public float SkullAnimationSpeed { get; set; } = 5f;

        // =======================
        // Kill Type Toggles
        // =======================
        public bool EnableNormalKill { get; set; } = true;
        public bool EnableHeadshotKill { get; set; } = true;
        public bool EnableExplosionKill { get; set; } = true;
        public bool EnableCritKill { get; set; } = true;

        // =======================
        // Ring Effect (ported from IconRingEffect)
        // =======================
        public bool EnableRingEffectCrit { get; set; } = true;
        public string RingEffectCritColor { get; set; } = "#9CCC65";
        public float RingEffectCritRadius { get; set; } = 42.0f;
        public float RingEffectCritThickness { get; set; } = 1.8f;

        public bool EnableRingEffectHeadshot { get; set; } = true;
        public string RingEffectHeadshotColor { get; set; } = "#D4B800";
        public float RingEffectHeadshotRadius { get; set; } = 42.0f;
        public float RingEffectHeadshotThickness { get; set; } = 3.0f;

        public bool EnableRingEffectExplosion { get; set; } = true;
        public string RingEffectExplosionColor { get; set; } = "#F77F00";
        public float RingEffectExplosionRadius { get; set; } = 42.0f;
        public float RingEffectExplosionThickness { get; set; } = 5.4f;

        /// <summary>Ring effect delay in seconds before it starts expanding.</summary>
        public float RingEffectDelay { get; set; } = 0.1f;
        /// <summary>Ring effect total duration from start to finish in seconds.</summary>
        public float RingEffectDuration { get; set; } = 0.3f;
        /// <summary>For explosion, delay before the second ring starts.</summary>
        public float ExplosionSecondRingDelay { get; set; } = 0.1f;

        // =======================
        // Advanced Effects (Valorant Style Glow)
        // =======================
        public bool EnableIconGlow { get; set; } = false;
        public string ColorIconGlow { get; set; } = "#FFFFFF";
        public float IconGlowIntensity { get; set; } = 0.45f;
        public float IconGlowSize { get; set; } = 4.0f;

        // =======================
        // Accent Tint (for texture coloration)
        // =======================
        public bool EnableAccentTint { get; set; } = false;
        public string ColorAccent { get; set; } = "#908CCD";

        // =======================
        // Text colors
        // =======================
        public string ColorPlayerName { get; set; } = "#FF4C4C";
        public string ColorFaction { get; set; } = "#FFFFFF";
        public string ColorKillDetails { get; set; } = "#CCCCCC";
        public string ColorExperience { get; set; } = "#FFFFCC";

        // =======================
        // Helper: parse hex color string to Unity Color
        // =======================
        public static Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            hex = hex.TrimStart('#');
            if (hex.Length < 6) return fallback;
            try
            {
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }
            catch
            {
                return fallback;
            }
        }

        public Color GetRingCritColor() => ParseHexColor(RingEffectCritColor, new Color(0.612f, 0.8f, 0.396f));
        public Color GetRingHeadshotColor() => ParseHexColor(RingEffectHeadshotColor, new Color(0.831f, 0.722f, 0f));
        public Color GetRingExplosionColor() => ParseHexColor(RingEffectExplosionColor, new Color(0.969f, 0.498f, 0f));
        public Color GetGlowColor() => ParseHexColor(ColorIconGlow, Color.white);
        public Color GetAccentColor() => ParseHexColor(ColorAccent, new Color(0.565f, 0.549f, 0.804f));
        public Color GetPlayerNameColor() => ParseHexColor(ColorPlayerName, new Color(1f, 0.3f, 0.3f));
        public Color GetFactionColor() => ParseHexColor(ColorFaction, Color.white);
        public Color GetDetailsColor() => ParseHexColor(ColorKillDetails, new Color(0.8f, 0.8f, 0.8f));
        public Color GetExperienceColor() => ParseHexColor(ColorExperience, new Color(1f, 1f, 0.8f));
    }
}
