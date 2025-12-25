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
                PruneDamageEntriesLocked(now);
                _damageEntries.Add(new DamageDisplayEntry
                {
                    Damage = damageAmount,
                    IsHeadshot = isHeadshot,
                    Timestamp = now
                });
            }
        }

        private void PruneDamageEntriesLocked(float currentTime)
        {
            if (_damageEntries.Count == 0) return;

            float lifetime = _config.HitDuration.Value;
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
            if (_hitDetected && Time.time - _hitTime < _config.HitDuration.Value)
            {
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

                if (_config.ShowDamageText.Value)
                {
                    List<DamageDisplayEntry> entriesSnapshot;
                    lock (_damageEntriesLock)
                    {
                        entriesSnapshot = new List<DamageDisplayEntry>(_damageEntries);
                    }

                    if (entriesSnapshot.Count > 0)
                    {
                        try
                        {
                            DrawDamageNumbers(drawRect, entriesSnapshot);
                        }
                        catch (Exception ex)
                        {
                            _log?.LogDebug($"[SimpleHitMarker] Failed to draw damage numbers: {ex}");
                        }
                    }
                }

                GUI.color = originalColor;
            }
            else if (_hitDetected)
            {
                _hitDetected = false;
                ClearDamageEntries();
            }
        }

        private void DrawDamageNumbers(Rect iconRect, List<DamageDisplayEntry> entries)
        {
            Color damageColor = _config.DamageTextColor.Value;
            int fontSize = Mathf.Max(1, _config.DamageTextSize.Value);

            GUIStyle fillStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = fontSize
            };
            fillStyle.normal.textColor = new Color(damageColor.r, damageColor.g, damageColor.b, 1f);

            GUIStyle outlineStyle = new GUIStyle(fillStyle);

            float paddingFromMarker = _config.DamageTextPadding.Value;
            float spacingBetweenNumbers = Mathf.Clamp(_config.DamageMultiTextPadding.Value, 0f, 200f);
            float outlineAlpha = Mathf.Clamp01(_config.DamageTextOutlineOpacity.Value);
            float outlineThickness = Mathf.Clamp(_config.DamageTextOutlineThickness.Value, 0.5f, 10f);
            float currentX = iconRect.xMax + paddingFromMarker;
            float centerY = iconRect.center.y;

            GUIContent content = new GUIContent();

            foreach (var entry in entries)
            {
                content.text = entry.Damage.ToString("0");
                Vector2 textSize = fillStyle.CalcSize(content);
                Rect textRect = new Rect(
                    currentX,
                    centerY - textSize.y / 2f,
                    textSize.x,
                    textSize.y
                );

                Color baseOutlineColor = entry.IsHeadshot
                    ? _config.DamageTextHeadshotOutlineColor.Value
                    : _config.DamageTextOutlineColor.Value;
                baseOutlineColor.a *= outlineAlpha;
                outlineStyle.normal.textColor = baseOutlineColor;

                DrawOutlinedLabel(textRect, content, fillStyle, outlineStyle, outlineThickness);
                currentX += textSize.x + spacingBetweenNumbers;
            }
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
