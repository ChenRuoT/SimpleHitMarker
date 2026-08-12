using UnityEngine;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// Ring effect renderer for kill icons, ported from gd656killicon's IconRingEffect.
    /// Renders an expanding/fading ring around the skull icon on headshot, explosion, or crit kills.
    /// Designed to be used inside Unity's OnGUI via GL API.
    /// </summary>
    public class IconRingEffect
    {
        private const int Segments = 72;
        private static Material _ringMaterial;

        private float _scale = 1.0f;

        // Ring parameters (set from preset or config)
        public float CritRadius { get; set; } = 42.0f;
        public float CritThickness { get; set; } = 1.8f;
        public Color CritColor { get; set; } = new Color(0.612f, 0.8f, 0.396f);

        public float HeadshotRadius { get; set; } = 42.0f;
        public float HeadshotThickness { get; set; } = 3.0f;
        public Color HeadshotColor { get; set; } = new Color(0.831f, 0.722f, 0f);

        public float ExplosionRadius { get; set; } = 42.0f;
        public float ExplosionThickness { get; set; } = 5.4f;
        public Color ExplosionColor { get; set; } = new Color(0.969f, 0.498f, 0f);

        public float EffectDelay { get; set; } = 0.1f;
        public float EffectDuration { get; set; } = 0.3f;
        public float ExplosionSecondRingDelay { get; set; } = 0.1f;

        private KillType _killType = KillType.Normal;
        private float _effectStartTime = -1f;
        private bool _enabled;

        public void SetScale(float scale)
        {
            _scale = scale;
        }

        /// <summary>
        /// Trigger a ring effect for a specific kill type.
        /// </summary>
        public void Trigger(float currentTime, bool enabled, KillType killType)
        {
            _enabled = enabled;
            _killType = killType;

            if (!enabled || (killType != KillType.Headshot && killType != KillType.Explosion && killType != KillType.Crit))
            {
                _effectStartTime = -1f;
                return;
            }
            _effectStartTime = currentTime + EffectDelay;
        }

        /// <summary>
        /// Render the ring effect at the given screen-space center position. Call inside OnGUI.
        /// </summary>
        public void Render(float centerX, float centerY, float currentTime)
        {
            using var _perf = PerfProbe.Measure("GUI.Ring");
            if (_effectStartTime <= 0f) return;

            float elapsed = currentTime - _effectStartTime;
            float maxDuration = _killType == KillType.Explosion
                ? EffectDuration + ExplosionSecondRingDelay
                : EffectDuration;

            if (elapsed < 0f || elapsed > maxDuration) return;

            float t = Mathf.Clamp01(elapsed / EffectDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            float alpha = 1f - t;
            alpha *= alpha;

            if (_killType == KillType.Headshot)
            {
                float radius = ResolveRadius(HeadshotRadius, eased);
                float thickness = HeadshotThickness * (1f - t) * _scale;
                DrawRing(centerX, centerY, radius, thickness, HeadshotColor, alpha);
                return;
            }

            if (_killType == KillType.Explosion)
            {
                // First ring (headshot-style)
                float radius1 = ResolveRadius(HeadshotRadius, eased);
                float thickness1 = HeadshotThickness * (1f - t) * _scale;
                DrawRing(centerX, centerY, radius1, thickness1, HeadshotColor, alpha);

                // Second ring after delay
                float ring2Elapsed = elapsed - ExplosionSecondRingDelay;
                if (ring2Elapsed < 0f || ring2Elapsed > EffectDuration) return;

                float t2 = Mathf.Clamp01(ring2Elapsed / EffectDuration);
                float eased2 = 1f - Mathf.Pow(1f - t2, 3f);
                float alpha2 = 1f - t2;
                alpha2 *= alpha2;
                float radius2 = ResolveRadius(ExplosionRadius, eased2);
                float thickness2 = ExplosionThickness * (1f - t2) * _scale;
                DrawRing(centerX, centerY, radius2, thickness2, ExplosionColor, alpha2);
                return;
            }

            if (_killType == KillType.Crit)
            {
                float radius = ResolveRadius(CritRadius, eased);
                float thickness = CritThickness * (1f - t) * _scale;
                DrawRing(centerX, centerY, radius, thickness, CritColor, alpha);
            }
        }

        private float ResolveRadius(float maxRadius, float eased)
        {
            const float baseRatio = 10f / (10f + 32f); // BASE_RADIUS / (BASE_RADIUS + RADIUS_GROWTH) from reference
            float minRadius = maxRadius * baseRatio;
            return (minRadius + (maxRadius - minRadius) * eased) * _scale;
        }

        /// <summary>
        /// Draw a ring using Unity's GL API (works inside OnGUI).
        /// </summary>
        private static void DrawRing(float centerX, float centerY, float radius, float thickness, Color color, float alpha)
        {
            if (thickness <= 0f || alpha <= 0f || radius <= 0f) return;

            float rOuter = radius + thickness * 0.5f;
            float rInner = Mathf.Max(0f, radius - thickness * 0.5f);

            Color ringColor = new Color(color.r, color.g, color.b, color.a * alpha);

            if (_ringMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    // Fallback: use GUI.DrawTexture approach or skip
                    return;
                }
                _ringMaterial = new Material(shader);
                _ringMaterial.hideFlags = HideFlags.HideAndDontSave;
                _ringMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _ringMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _ringMaterial.SetInt("_Cull", 0);
                _ringMaterial.SetInt("_ZWrite", 0);
            }

            _ringMaterial.SetPass(0);

            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLE_STRIP);
            GL.Color(ringColor);

            for (int i = 0; i <= Segments; i++)
            {
                float angle = Mathf.PI * 2f * i / Segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float xo = centerX + cos * rOuter;
                float yo = centerY + sin * rOuter;
                float xi = centerX + cos * rInner;
                float yi = centerY + sin * rInner;

                GL.Vertex3(xo, yo, 0f);
                GL.Vertex3(xi, yi, 0f);
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>
        /// Check if the effect is currently active (for external checks).
        /// </summary>
        public bool IsActive(float currentTime)
        {
            if (_effectStartTime <= 0f) return false;
            float maxDuration = _killType == KillType.Explosion
                ? EffectDuration + ExplosionSecondRingDelay
                : EffectDuration;
            return (currentTime - _effectStartTime) >= 0f && (currentTime - _effectStartTime) <= maxDuration;
        }
    }
}
