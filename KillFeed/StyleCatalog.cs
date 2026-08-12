using System.Collections.Generic;
using UnityEngine;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// Catalog of built-in kill icon preset styles, ported from gd656killicon's ValorantStyleCatalog.
    /// Defines Tarkov-themed styles with accent colors and effect parameters.
    /// </summary>
    public static class StyleCatalog
    {
        // Style IDs
        public const string STYLE_TACTICAL = "tactical";
        public const string STYLE_OPERATOR = "operator";
        public const string STYLE_BLOODBATH = "bloodbath";
        public const string STYLE_HEADHUNTER = "headhunter";
        public const string STYLE_SLICK = "slick";
        public const string STYLE_NIGHTOPS = "nightops";
        public const string STYLE_INFERNO = "inferno";
        public const string STYLE_PLAGUE = "plague";
        public const string STYLE_DEFAULT = STYLE_TACTICAL;

        /// <summary>
        /// Represents a single style's predefined visual parameters.
        /// </summary>
        public class StyleSpec
        {
            public string StyleId { get; set; }
            public string DisplayName { get; set; }
            public Color AccentColor { get; set; }
            public Color RingHeadshotColor { get; set; }
            public Color RingCritColor { get; set; }
            public Color RingExplosionColor { get; set; }
            public Color GlowColor { get; set; }
            public Color NameColor { get; set; }
            public Color FactionColor { get; set; }
            public Color DetailsColor { get; set; }
            public Color ExperienceColor { get; set; }
            public float RingHeadshotThickness { get; set; }
            public float RingCritThickness { get; set; }
            public float RingExplosionThickness { get; set; }
            public bool EnableGlow { get; set; }
            public float GlowIntensity { get; set; }
            public float GlowSize { get; set; }
            public float SkullSize { get; set; }
            public string Description { get; set; }
        }

        private static readonly Dictionary<string, StyleSpec> _styles = new Dictionary<string, StyleSpec>();

        static StyleCatalog()
        {
            RegisterStyles();
        }

        private static void RegisterStyles()
        {
            // === Tactical (Default) - Military green ===
            Register(new StyleSpec
            {
                StyleId = STYLE_TACTICAL,
                DisplayName = "Tactical",
                AccentColor = new Color(0.557f, 0.698f, 0.392f),     // #8EB264
                RingHeadshotColor = new Color(0.831f, 0.722f, 0f),    // #D4B800 (gold)
                RingCritColor = new Color(0.612f, 0.8f, 0.396f),      // #9CCC65 (green)
                RingExplosionColor = new Color(0.969f, 0.498f, 0f),   // #F77F00 (orange)
                GlowColor = new Color(0.557f, 0.698f, 0.392f),
                NameColor = new Color(1f, 0.3f, 0.3f),                // #FF4C4C
                FactionColor = Color.white,
                DetailsColor = new Color(0.8f, 0.8f, 0.8f),
                ExperienceColor = new Color(1f, 1f, 0.8f),
                RingHeadshotThickness = 3.0f,
                RingCritThickness = 1.8f,
                RingExplosionThickness = 5.4f,
                EnableGlow = false,
                GlowIntensity = 0.45f,
                GlowSize = 4.0f,
                SkullSize = 64f,
                Description = "Military green tactical style. Clean and functional."
            });

            // === Operator - Dark/stealth ===
            Register(new StyleSpec
            {
                StyleId = STYLE_OPERATOR,
                DisplayName = "Operator",
                AccentColor = new Color(0.2f, 0.2f, 0.22f),           // #333338
                RingHeadshotColor = new Color(0.9f, 0.15f, 0.15f),    // #E62626 (deep red)
                RingCritColor = new Color(0.4f, 0.75f, 0.85f),        // #66BFD9 (cyan)
                RingExplosionColor = new Color(0.95f, 0.4f, 0.05f),   // #F2660D (deep orange)
                GlowColor = new Color(0.9f, 0.15f, 0.15f),
                NameColor = new Color(0.9f, 0.15f, 0.15f),
                FactionColor = new Color(0.7f, 0.7f, 0.75f),
                DetailsColor = new Color(0.55f, 0.55f, 0.6f),
                ExperienceColor = new Color(0.9f, 0.85f, 0.7f),
                RingHeadshotThickness = 3.5f,
                RingCritThickness = 2.0f,
                RingExplosionThickness = 6.0f,
                EnableGlow = true,
                GlowIntensity = 0.35f,
                GlowSize = 3.5f,
                SkullSize = 68f,
                Description = "Dark stealth operator style with red accent glow."
            });

            // === Bloodbath - Intense red ===
            Register(new StyleSpec
            {
                StyleId = STYLE_BLOODBATH,
                DisplayName = "Bloodbath",
                AccentColor = new Color(0.85f, 0.08f, 0.08f),        // #D91414
                RingHeadshotColor = new Color(0.95f, 0.2f, 0.1f),    // #F2331A
                RingCritColor = new Color(0.85f, 0.08f, 0.08f),
                RingExplosionColor = new Color(1f, 0.35f, 0f),       // #FF5900
                GlowColor = new Color(0.85f, 0.08f, 0.08f),
                NameColor = new Color(0.95f, 0.2f, 0.1f),
                FactionColor = new Color(0.9f, 0.5f, 0.4f),
                DetailsColor = new Color(0.85f, 0.4f, 0.35f),
                ExperienceColor = new Color(1f, 0.7f, 0.5f),
                RingHeadshotThickness = 3.5f,
                RingCritThickness = 2.0f,
                RingExplosionThickness = 6.5f,
                EnableGlow = true,
                GlowIntensity = 0.5f,
                GlowSize = 5.0f,
                SkullSize = 70f,
                Description = "Brutal blood-red style. Maximum visual impact."
            });

            // === Headhunter - Gold/bounty hunter ===
            Register(new StyleSpec
            {
                StyleId = STYLE_HEADHUNTER,
                DisplayName = "Headhunter",
                AccentColor = new Color(0.831f, 0.686f, 0.216f),     // #D4AF37 (gold)
                RingHeadshotColor = new Color(0.95f, 0.8f, 0.05f),   // #F2CC0D (bright gold)
                RingCritColor = new Color(0.6f, 0.8f, 0.35f),
                RingExplosionColor = new Color(0.9f, 0.35f, 0.05f),
                GlowColor = new Color(0.831f, 0.686f, 0.216f),
                NameColor = new Color(0.95f, 0.8f, 0.05f),
                FactionColor = new Color(0.9f, 0.85f, 0.7f),
                DetailsColor = new Color(0.8f, 0.7f, 0.5f),
                ExperienceColor = new Color(1f, 0.9f, 0.4f),
                RingHeadshotThickness = 4.0f,
                RingCritThickness = 2.2f,
                RingExplosionThickness = 5.5f,
                EnableGlow = true,
                GlowIntensity = 0.55f,
                GlowSize = 5.5f,
                SkullSize = 66f,
                Description = "Golden bounty hunter theme. Headshots feel rewarding."
            });

            // === Slick - Clean blue/cyan ===
            Register(new StyleSpec
            {
                StyleId = STYLE_SLICK,
                DisplayName = "Slick",
                AccentColor = new Color(0.15f, 0.55f, 0.85f),        // #268CD9
                RingHeadshotColor = new Color(0.2f, 0.7f, 0.95f),    // #33B2F2
                RingCritColor = new Color(0.3f, 0.8f, 0.55f),        // #4DCC8C
                RingExplosionColor = new Color(0.15f, 0.55f, 0.95f),
                GlowColor = new Color(0.15f, 0.55f, 0.85f),
                NameColor = new Color(0.2f, 0.7f, 0.95f),
                FactionColor = new Color(0.7f, 0.8f, 0.95f),
                DetailsColor = new Color(0.6f, 0.7f, 0.8f),
                ExperienceColor = new Color(0.7f, 0.9f, 1f),
                RingHeadshotThickness = 2.8f,
                RingCritThickness = 1.8f,
                RingExplosionThickness = 5.0f,
                EnableGlow = true,
                GlowIntensity = 0.4f,
                GlowSize = 4.5f,
                SkullSize = 62f,
                Description = "Clean blue slick style. Modern and minimal."
            });

            // === NightOps - Purple stealth ===
            Register(new StyleSpec
            {
                StyleId = STYLE_NIGHTOPS,
                DisplayName = "Night Ops",
                AccentColor = new Color(0.5f, 0.2f, 0.7f),           // #8033B2
                RingHeadshotColor = new Color(0.7f, 0.25f, 0.9f),    // #B240E5
                RingCritColor = new Color(0.45f, 0.6f, 0.9f),        // #7399E5
                RingExplosionColor = new Color(0.85f, 0.3f, 0.55f),  // #D94D8C
                GlowColor = new Color(0.5f, 0.2f, 0.7f),
                NameColor = new Color(0.7f, 0.25f, 0.9f),
                FactionColor = new Color(0.75f, 0.65f, 0.9f),
                DetailsColor = new Color(0.6f, 0.5f, 0.7f),
                ExperienceColor = new Color(0.85f, 0.7f, 1f),
                RingHeadshotThickness = 3.2f,
                RingCritThickness = 2.0f,
                RingExplosionThickness = 5.8f,
                EnableGlow = true,
                GlowIntensity = 0.5f,
                GlowSize = 5.0f,
                SkullSize = 66f,
                Description = "Purple night operations. Distinctive and stylish."
            });

            // === Inferno - Fire/orange ===
            Register(new StyleSpec
            {
                StyleId = STYLE_INFERNO,
                DisplayName = "Inferno",
                AccentColor = new Color(0.9f, 0.35f, 0.05f),         // #E5590D
                RingHeadshotColor = new Color(1f, 0.45f, 0.05f),     // #FF730D
                RingCritColor = new Color(0.85f, 0.55f, 0.1f),       // #D98C1A
                RingExplosionColor = new Color(1f, 0.2f, 0f),        // #FF3300
                GlowColor = new Color(0.9f, 0.35f, 0.05f),
                NameColor = new Color(1f, 0.45f, 0.05f),
                FactionColor = new Color(0.95f, 0.75f, 0.5f),
                DetailsColor = new Color(0.85f, 0.55f, 0.35f),
                ExperienceColor = new Color(1f, 0.8f, 0.4f),
                RingHeadshotThickness = 3.5f,
                RingCritThickness = 2.0f,
                RingExplosionThickness = 7.0f,
                EnableGlow = true,
                GlowIntensity = 0.55f,
                GlowSize = 6.0f,
                SkullSize = 70f,
                Description = "Blazing fire theme. Explosions feel massive."
            });

            // === Plague - Toxic green ===
            Register(new StyleSpec
            {
                StyleId = STYLE_PLAGUE,
                DisplayName = "Plague",
                AccentColor = new Color(0.35f, 0.75f, 0.15f),        // #59BF26
                RingHeadshotColor = new Color(0.55f, 0.85f, 0.1f),   // #8CD91A
                RingCritColor = new Color(0.35f, 0.75f, 0.15f),
                RingExplosionColor = new Color(0.6f, 0.7f, 0.05f),   // #99B20D
                GlowColor = new Color(0.35f, 0.75f, 0.15f),
                NameColor = new Color(0.55f, 0.85f, 0.1f),
                FactionColor = new Color(0.7f, 0.85f, 0.55f),
                DetailsColor = new Color(0.55f, 0.7f, 0.4f),
                ExperienceColor = new Color(0.75f, 0.95f, 0.4f),
                RingHeadshotThickness = 3.0f,
                RingCritThickness = 2.5f,
                RingExplosionThickness = 6.0f,
                EnableGlow = true,
                GlowIntensity = 0.45f,
                GlowSize = 4.5f,
                SkullSize = 64f,
                Description = "Toxic green plague theme. Sick and radioactive."
            });
        }

        private static void Register(StyleSpec spec)
        {
            _styles[spec.StyleId] = spec;
        }

        /// <summary>
        /// Get a style by its ID. Returns the default style if not found.
        /// </summary>
        public static StyleSpec GetStyle(string styleId)
        {
            if (string.IsNullOrEmpty(styleId)) return _styles[STYLE_DEFAULT];
            string normalized = styleId.ToLowerInvariant().Trim();

            // Allow fuzzy matching of common names
            normalized = normalized switch
            {
                "default" => STYLE_DEFAULT,
                "military" => STYLE_TACTICAL,
                "green" => STYLE_TACTICAL,
                "dark" => STYLE_OPERATOR,
                "stealth" => STYLE_OPERATOR,
                "red" => STYLE_BLOODBATH,
                "blood" => STYLE_BLOODBATH,
                "gold" => STYLE_HEADHUNTER,
                "bounty" => STYLE_HEADHUNTER,
                "blue" => STYLE_SLICK,
                "cyan" => STYLE_SLICK,
                "purple" => STYLE_NIGHTOPS,
                "night" => STYLE_NIGHTOPS,
                "fire" => STYLE_INFERNO,
                "orange" => STYLE_INFERNO,
                "toxic" => STYLE_PLAGUE,
                "green_toxic" => STYLE_PLAGUE,
                _ => normalized
            };

            return _styles.TryGetValue(normalized, out var style) ? style : _styles[STYLE_DEFAULT];
        }

        /// <summary>
        /// Get all registered style IDs.
        /// </summary>
        public static IEnumerable<string> GetAllStyleIds()
        {
            return _styles.Keys;
        }

        /// <summary>
        /// Get all registered styles.
        /// </summary>
        public static IEnumerable<StyleSpec> GetAllStyles()
        {
            return _styles.Values;
        }

        /// <summary>
        /// Apply a StyleSpec to a KillIconPreset, filling in visual parameters.
        /// </summary>
        public static void ApplyStyleToPreset(StyleSpec style, KillIconPreset preset)
        {
            if (style == null || preset == null) return;

            preset.RingEffectHeadshotColor = $"#{ColorUtility.ToHtmlStringRGB(style.RingHeadshotColor)}";
            preset.RingEffectCritColor = $"#{ColorUtility.ToHtmlStringRGB(style.RingCritColor)}";
            preset.RingEffectExplosionColor = $"#{ColorUtility.ToHtmlStringRGB(style.RingExplosionColor)}";
            preset.ColorIconGlow = $"#{ColorUtility.ToHtmlStringRGB(style.GlowColor)}";
            preset.ColorPlayerName = $"#{ColorUtility.ToHtmlStringRGB(style.NameColor)}";
            preset.ColorFaction = $"#{ColorUtility.ToHtmlStringRGB(style.FactionColor)}";
            preset.ColorKillDetails = $"#{ColorUtility.ToHtmlStringRGB(style.DetailsColor)}";
            preset.ColorExperience = $"#{ColorUtility.ToHtmlStringRGB(style.ExperienceColor)}";
            preset.ColorAccent = $"#{ColorUtility.ToHtmlStringRGB(style.AccentColor)}";

            preset.RingEffectHeadshotThickness = style.RingHeadshotThickness;
            preset.RingEffectCritThickness = style.RingCritThickness;
            preset.RingEffectExplosionThickness = style.RingExplosionThickness;

            preset.EnableIconGlow = style.EnableGlow;
            preset.IconGlowIntensity = style.GlowIntensity;
            preset.IconGlowSize = style.GlowSize;

            preset.SkullSize = style.SkullSize;
            preset.DisplayName = style.DisplayName;
        }
    }
}
