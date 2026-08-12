using UnityEngine;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// Glow render effect for kill icons, ported from gd656killicon's IconGlowRenderEffect.
    /// Renders a colored glow around a texture by drawing offset copies with additive blending.
    /// Call inside Unity's OnGUI.
    /// </summary>
    public class IconGlowEffect
    {
        /// <summary>
        /// 8-directional offsets for glow spread, matching the reference.
        /// </summary>
        private static readonly Vector2[] Offsets = new Vector2[]
        {
            new Vector2(-1f, -1f),
            new Vector2( 0f, -1f),
            new Vector2( 1f, -1f),
            new Vector2(-1f,  0f),
            new Vector2( 1f,  0f),
            new Vector2(-1f,  1f),
            new Vector2( 0f,  1f),
            new Vector2( 1f,  1f)
        };

        // Static material used for all glow passes
        private static Material _glowMaterial;

        public bool Enabled { get; set; }
        public Color GlowColor { get; set; } = Color.white;
        public float Intensity { get; set; } = 0.45f;
        public float Size { get; set; } = 4.0f;

        /// <summary>
        /// Draw a glow around a texture region. The texture is drawn multiple times 
        /// at offset positions with additive blending to create a glow halo.
        /// </summary>
        /// <param name="texture">The texture to glow around.</param>
        /// <param name="rect">Screen-space rect for the main icon.</param>
        /// <param name="alpha">Overall alpha multiplier for the glow.</param>
        public void DrawGlow(Texture2D texture, Rect rect, float alpha)
        {
            using var _perf = PerfProbe.Measure("GUI.Glow");
            if (!Enabled || texture == null || alpha <= 0.001f || Intensity <= 0.001f || Size <= 0.01f)
                return;

            float glowAlpha = Mathf.Clamp01(alpha * Intensity);
            float outerSpread = Mathf.Max(0.5f, Size);
            float innerSpread = outerSpread * 0.55f;

            // Use additive blending for glow (SrcAlpha, One)
            if (_glowMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return;
                _glowMaterial = new Material(shader);
                _glowMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            Color originalColor = GUI.color;

            // Outer ring (8 directions, lower opacity)
            float outerPassAlpha = glowAlpha * 0.16f;
            GUI.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, outerPassAlpha);
            foreach (var offset in Offsets)
            {
                Rect outerRect = new Rect(
                    rect.x + offset.x * outerSpread,
                    rect.y + offset.y * outerSpread,
                    rect.width,
                    rect.height
                );
                GUI.DrawTexture(outerRect, texture);
            }

            // Inner ring (8 directions, medium opacity)
            float innerPassAlpha = glowAlpha * 0.11f;
            GUI.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, innerPassAlpha);
            foreach (var offset in Offsets)
            {
                Rect innerRect = new Rect(
                    rect.x + offset.x * innerSpread,
                    rect.y + offset.y * innerSpread,
                    rect.width,
                    rect.height
                );
                GUI.DrawTexture(innerRect, texture);
            }

            // Center pass (very light)
            GUI.color = new Color(GlowColor.r, GlowColor.g, GlowColor.b, glowAlpha * 0.09f);
            GUI.DrawTexture(rect, texture);

            GUI.color = originalColor;
        }
    }
}
