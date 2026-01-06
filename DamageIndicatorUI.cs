using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using BepInEx.Logging;

namespace SimpleHitMarker
{
    public class DamageIndicatorUI
    {
        public class DamageDisplayEntry
        {
            public float Damage;
            public bool IsHeadshot;
            public float Timestamp;
        }

        private readonly ConfigurationManager _config;
        private readonly ManualLogSource _log;

        private readonly object _damageEntriesLock = new object();
        private readonly List<DamageDisplayEntry> _damageEntries = new List<DamageDisplayEntry>();

        private Texture2D _hitTexture;

        private bool _hitDetected = false;
        private float _hitTime = 0f;

        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, -1f),
            new Vector2(0f, 1f),
            new Vector2(-1f, -1f),
            new Vector2(-1f, 1f),
            new Vector2(1f, -1f),
            new Vector2(1f, 1f)
        };

        public DamageIndicatorUI(ConfigurationManager config, ManualLogSource log)
        {
            _config = config;
            _log = log;
            LoadHitTexture();
        }

        private void LoadHitTexture()
        {
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string hitPngpath = Path.Combine(assemblyDir, "SimpleHitMarker", "hit.png");
                _log?.LogInfo($"[SimpleHitMarker] Looking for hit texture at: {hitPngpath}");
                _hitTexture = TextureLoader.LoadTextureFromFile(hitPngpath);
                if (_hitTexture == null)
                {
                    string alt = Path.Combine(assemblyDir, "hit.png");
                    _log?.LogInfo($"[SimpleHitMarker] Trying alternate path: {alt}");
                    _hitTexture = TextureLoader.LoadTextureFromFile(alt);
                }

                if (_hitTexture == null)
                {
                    _log?.LogWarning("[SimpleHitMarker] hit.png not found. Using simple X fallback.");
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[SimpleHitMarker] Texture load error: {ex}");
            }
        }

        public void RegisterHit(float damage, bool isHeadshot)
        {
            _hitDetected = true;
            _hitTime = Time.time;
            AddDamageEntry(damage, isHeadshot);
        }

        private void AddDamageEntry(float damageAmount, bool isHeadshot)
        {
            float now = Time.time;
            lock (_damageEntriesLock)
            {
                // Clean up expired entries first
                PruneDamageEntriesLocked(now);
                // Insert new entry at the FRONT (index 0) so it appears closest to the marker
                _damageEntries.Insert(0, new DamageDisplayEntry
                {
                    Damage = damageAmount,
                    IsHeadshot = isHeadshot,
                    Timestamp = now
                });
            }
        }

        private void PruneDamageEntriesLocked(float currentTime)
        {
            float lifetime = _config.HitDuration.Value;
            // Iterate backwards to safely remove
            for (int i = _damageEntries.Count - 1; i >= 0; i--)
            {
                if (currentTime - _damageEntries[i].Timestamp > lifetime)
                {
                    _damageEntries.RemoveAt(i);
                }
            }
        }

        public void ClearDamageEntries()
        {
            lock (_damageEntriesLock)
            {
                _damageEntries.Clear();
            }
        }

        public void OnGUI()
        {
            // --- 1. Draw Hit Marker Icon ---
            // The icon's visibility is controlled by the LAST hit time.
            if (_hitDetected && Time.time - _hitTime < _config.HitDuration.Value)
            {
                DrawHitMarkerIcon();
            }
            else
            {
                if (_hitDetected)
                {
                    _hitDetected = false;
                    // Note: We DO NOT clear damage entries here anymore. 
                    // They have their own independent lifetimes.
                }
            }

            // --- 2. Draw Damage Numbers ---
            // Always check for damage numbers, regardless of whether the icon is currently visible.
            if (_config.ShowDamageText.Value)
            {
                DrawDamageNumbers();
            }
        }

        private void DrawHitMarkerIcon()
        {
            if (!_config.EnableHitMarker.Value)
            {
                return;
            }

            float hitDurationValue = _config.HitDuration.Value;
            float t = (Time.time - _hitTime) / hitDurationValue;
            float alpha = 1f - t;
            float animationScale = _config.HitMarkerAnimationScale.Value;
            float scale = Mathf.Lerp(animationScale, 1f, t);
            float size = _config.HitBaseSize.Value * scale;

            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            Vector2 center = new Vector2(
                Screen.width * 0.5f + _config.HitMarkerCenterOffset.Value.x,
                Screen.height * 0.5f + _config.HitMarkerCenterOffset.Value.y
            );

            Rect drawRect = new Rect(center.x - size / 2f, center.y - size / 2f, size, size);

            if (_hitTexture != null)
            {
                GUI.DrawTexture(drawRect, _hitTexture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                float w = size;
                float h = 3f;
                GUI.DrawTexture(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), Texture2D.whiteTexture);
                GUIUtility.RotateAroundPivot(45f, center);
                GUI.DrawTexture(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), Texture2D.whiteTexture);
                GUIUtility.RotateAroundPivot(-45f, center);
            }

            GUI.color = originalColor;
        }

        private void DrawDamageNumbers()
        {
            List<DamageDisplayEntry> entriesSnapshot;
            float now = Time.time;

            lock (_damageEntriesLock)
            {
                // Optional: Prune again to ensure we don't draw extremely old stuff
                PruneDamageEntriesLocked(now);
                entriesSnapshot = new List<DamageDisplayEntry>(_damageEntries);
            }

            if (entriesSnapshot.Count == 0) return;

            // Prepare styles
            Color damageColor = _config.DamageTextColor.Value;
            int fontSize = Mathf.Max(1, _config.DamageTextSize.Value);

            GUIStyle fillStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = fontSize
            };

            // Outline style
            GUIStyle outlineStyle = new GUIStyle(fillStyle);

            float lifetime = _config.HitDuration.Value;

            // Calculate starting position (anchor)
            // We reuse the center calculation to align with the marker
            Vector2 center = new Vector2(
                Screen.width * 0.5f + _config.HitMarkerCenterOffset.Value.x,
                Screen.height * 0.5f + _config.HitMarkerCenterOffset.Value.y
            );

            // The icon size is needed to know where to start drawing text (to the right of the icon)
            // Even if icon is not visible (faded out), we calculate where it *would* be.
            // We use the base size here, avoiding the animation scale for text stability, 
            // or we could use the current animated size if we want text to move with it. 
            // Let's use base size for stability.
            float iconSize = _config.HitBaseSize.Value;

            float paddingFromMarker = _config.DamageTextPadding.Value;
            float spacingBetweenNumbers = Mathf.Clamp(_config.DamageMultiTextPadding.Value, 0f, 200f);
            float globalOutlineAlpha = Mathf.Clamp01(_config.DamageTextOutlineOpacity.Value);
            float outlineThickness = Mathf.Clamp(_config.DamageTextOutlineThickness.Value, 0.5f, 10f);

            // Start drawing to the right of the icon
            float currentX = center.x + (iconSize / 2f) + paddingFromMarker;
            float centerY = center.y;

            GUIContent content = new GUIContent();
            Color originalGuiColor = GUI.color;

            // Iterate through entries. 
            // Since we Insert(0) new entries, the first item in the list is the NEWEST.
            // We want the newest item to be at 'currentX', and older items pushed further right.
            foreach (var entry in entriesSnapshot)
            {
                float age = now - entry.Timestamp;
                if (age >= lifetime) continue; // Should be caught by prune, but safety check

                float t = age / lifetime;
                float entryAlpha = 1f - t; // Fade out over time independently

                // Set content
                content.text = entry.Damage.ToString("0");
                Vector2 textSize = fillStyle.CalcSize(content);

                Rect textRect = new Rect(
                    currentX,
                    centerY - textSize.y / 2f,
                    textSize.x,
                    textSize.y
                );

                // Set colors with alpha
                Color currentTextColor = new Color(damageColor.r, damageColor.g, damageColor.b, entryAlpha);
                fillStyle.normal.textColor = currentTextColor;

                Color baseOutlineColor = entry.IsHeadshot
                    ? _config.DamageTextHeadshotOutlineColor.Value
                    : _config.DamageTextOutlineColor.Value;

                // Combine global outline opacity with the entry's fade status
                float finalOutlineAlpha = baseOutlineColor.a * globalOutlineAlpha * entryAlpha;
                outlineStyle.normal.textColor = new Color(baseOutlineColor.r, baseOutlineColor.g, baseOutlineColor.b, finalOutlineAlpha);

                DrawOutlinedLabel(textRect, content, fillStyle, outlineStyle, outlineThickness);

                // Push position to the right for the next (older) number
                currentX += textSize.x + spacingBetweenNumbers;
            }

            GUI.color = originalGuiColor;
        }

        public static void DrawOutlinedLabel(Rect rect, GUIContent content, GUIStyle fillStyle, GUIStyle outlineStyle, float outlineThickness)
        {
            foreach (var direction in OutlineDirections)
            {
                Rect outlineRect = new Rect(
                    rect.x + direction.x * outlineThickness,
                    rect.y + direction.y * outlineThickness,
                    rect.width,
                    rect.height
                );
                GUI.Label(outlineRect, content, outlineStyle);
            }

            GUI.Label(rect, content, fillStyle);
        }

        public void Cleanup()
        {
            if (_hitTexture != null)
            {
                UnityEngine.Object.Destroy(_hitTexture);
                _hitTexture = null;
            }
        }
    }
}
