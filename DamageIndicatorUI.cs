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

        // Cached IMGUI resources. Constructing GUIStyles inside OnGUI defeats Unity's text
        // generation cache — every GUI.Label against a fresh style re-measures and re-rasterizes
        // its glyphs. With 9 labels per number and 8 OnGUI events per frame that cost ~100ms on
        // the frame where a hit and a kill land together. See the IMGUI rules in CLAUDE.md.
        private bool _guiStylesInitialized;
        private GUIStyle _fillStyle;
        // private GUIStyle _outlineStyle;   // 描边样式暂时停用
        private int _cachedFontSize = -1;
        private readonly List<DamageDisplayEntry> _damageEntriesSnapshot = new List<DamageDisplayEntry>();
        private readonly GUIContent _damageContent = new GUIContent();

        // 字形预热：首次对某字号做 CalcSize 时，Unity 要光栅化字形并重建字体图集，
        // 实测单次耗时 ~76ms。若不预热，这笔开销正好落在"第一次命中"那一帧。
        private readonly GUIContent _primeContent = new GUIContent("0123456789");
        private volatile bool _fontPrimeRequested = true;
        private int _primedFontSize = -1;

        /// <summary>
        /// 请求重新预热字形。EFT 切场景后字体图集可能被回收，所以每次进图都要重来一次。
        /// 只写一个标志位，可从任意线程调用；真正的预热在 OnGUI（主线程）里做。
        /// </summary>
        public void InvalidateFontPrime()
        {
            _fontPrimeRequested = true;
        }

        /// <summary>
        /// 在 OnGUI 的 Repaint 事件里付掉字形光栅化成本。必须在 OnGUI 内调用（GUI.skin 才有效）。
        /// </summary>
        private void PrimeFontCache(int fontSize)
        {
            if (!_fontPrimeRequested && _primedFontSize == fontSize) return;

            _fontPrimeRequested = false;
            _primedFontSize = fontSize;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                _fillStyle.fontSize = fontSize;
                _cachedFontSize = fontSize;
                // 测量一次包含全部数字的字符串，一次性生成伤害数字会用到的所有字形。
                _fillStyle.CalcSize(_primeContent);

                sw.Stop();
                if (sw.ElapsedMilliseconds > 5)
                {
                    _log?.LogInfo($"[SimpleHitMarker] Damage font primed (size={fontSize}) in {sw.ElapsedMilliseconds} ms");
                }
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"[SimpleHitMarker] Damage font priming failed (non-critical): {ex}");
            }
        }

        public DamageIndicatorUI(ConfigurationManager config, ManualLogSource log)
        {
            _config = config;
            _log = log;
            LoadHitTexture();
        }

        /// <summary>
        /// Build the damage-number styles once. Must run inside OnGUI — GUI.skin is only valid there.
        /// </summary>
        private void EnsureGuiStyles()
        {
            if (_guiStylesInitialized) return;

            _fillStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft
            };
            // _outlineStyle = new GUIStyle(_fillStyle);   // 描边样式暂时停用

            _guiStylesInitialized = true;
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
            // --- 0. 字形预热 ---
            // 在空闲帧付掉字体图集的生成成本，否则它会落在第一次命中那一帧（实测 ~76ms）。
            // 预热后本方法每帧只是一次布尔判断。
            if (Event.current.type == EventType.Repaint)
            {
                EnsureGuiStyles();
                PrimeFontCache(Mathf.Max(1, _config.DamageTextSize.Value));
            }

            // --- 1. Draw Hit Marker Icon ---
            // The icon's visibility is controlled by the LAST hit time.
            if (_hitDetected && Time.time - _hitTime < _config.HitDuration.Value)
            {
                using (PerfProbe.Measure("Dmg.Icon"))
                {
                    DrawHitMarkerIcon();
                }
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

            // Texture drawing only takes effect on Repaint; skip the other ~7 events per frame.
            if (Event.current.type != EventType.Repaint) return;

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
            float now = Time.time;

            using (PerfProbe.Measure("Dmg.Snap"))
            {
                lock (_damageEntriesLock)
                {
                    // Optional: Prune again to ensure we don't draw extremely old stuff
                    PruneDamageEntriesLocked(now);
                    // Reuse the snapshot list instead of allocating one per OnGUI call.
                    _damageEntriesSnapshot.Clear();
                    _damageEntriesSnapshot.AddRange(_damageEntries);
                }
            }

            if (_damageEntriesSnapshot.Count == 0) return;

            // Text measurement and drawing only have an effect on Repaint. OnGUI runs ~8 times a
            // frame (Layout, input, Repaint), so bailing here skips the expensive CalcSize/Label
            // work on every non-drawing event.
            if (Event.current.type != EventType.Repaint) return;

            Color damageColor;
            using (PerfProbe.Measure("Dmg.Styles"))
            {
                EnsureGuiStyles();

                // Prepare styles
                damageColor = _config.DamageTextColor.Value;
                int fontSize = Mathf.Max(1, _config.DamageTextSize.Value);

                // Only assign when it actually changed — writing fontSize invalidates Unity's
                // cached text generation for the style.
                if (fontSize != _cachedFontSize)
                {
                    _cachedFontSize = fontSize;
                    _fillStyle.fontSize = fontSize;
                    // _outlineStyle.fontSize = fontSize;
                }
            }

            GUIStyle fillStyle = _fillStyle;
            // GUIStyle outlineStyle = _outlineStyle;

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
            // 描边样式暂时停用：它是 9 次 GUI.Label 中的 8 次，是本帧最大开销来源，
            // 且这套 style 只在配置里开了入口，并未真正接入预设(Preset/StyleCatalog)核心。
            // float globalOutlineAlpha = Mathf.Clamp01(_config.DamageTextOutlineOpacity.Value);
            // float outlineThickness = Mathf.Clamp(_config.DamageTextOutlineThickness.Value, 0.5f, 10f);

            // Start drawing to the right of the icon
            float currentX = center.x + (iconSize / 2f) + paddingFromMarker;
            float centerY = center.y;

            GUIContent content = _damageContent;
            Color originalGuiColor = GUI.color;

            // Iterate through entries.
            // Since we Insert(0) new entries, the first item in the list is the NEWEST.
            // We want the newest item to be at 'currentX', and older items pushed further right.
            for (int i = 0; i < _damageEntriesSnapshot.Count; i++)
            {
                var entry = _damageEntriesSnapshot[i];
                float age = now - entry.Timestamp;
                if (age >= lifetime) continue; // Should be caught by prune, but safety check

                // 护甲吸收了全部伤害时 DidBodyDamage 为 0，此时仍显示命中标记，
                // 但不绘制一个无意义的 "0"
                if (entry.Damage < 0.5f) continue;

                float t = age / lifetime;
                float entryAlpha = 1f - t; // Fade out over time independently

                // Set content
                content.text = entry.Damage.ToString("0");

                Vector2 textSize;
                using (PerfProbe.Measure("Dmg.CalcSize"))
                {
                    textSize = fillStyle.CalcSize(content);
                }

                Rect textRect = new Rect(
                    currentX,
                    centerY - textSize.y / 2f,
                    textSize.x,
                    textSize.y
                );

                // Set colors with alpha
                Color currentTextColor = new Color(damageColor.r, damageColor.g, damageColor.b, entryAlpha);
                fillStyle.normal.textColor = currentTextColor;

                // 描边暂时停用（见上方说明）。恢复时把下面这段取消注释，并改回 DrawOutlinedLabel。
                // Color baseOutlineColor = entry.IsHeadshot
                //     ? _config.DamageTextHeadshotOutlineColor.Value
                //     : _config.DamageTextOutlineColor.Value;
                //
                // // Combine global outline opacity with the entry's fade status
                // float finalOutlineAlpha = baseOutlineColor.a * globalOutlineAlpha * entryAlpha;
                // outlineStyle.normal.textColor = new Color(baseOutlineColor.r, baseOutlineColor.g, baseOutlineColor.b, finalOutlineAlpha);
                //
                // DrawOutlinedLabel(textRect, content, fillStyle, outlineStyle, outlineThickness);

                using (PerfProbe.Measure("Dmg.Label"))
                {
                    GUI.Label(textRect, content, fillStyle);
                }

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
