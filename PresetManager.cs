using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimpleHitMarker.KillFeed;
using UnityEngine;

namespace SimpleHitMarker
{
    /// <summary>
    /// Singleton manager for loading and caching Preset configs, Textures, and AudioClips
    /// from the file system. Integrates with StyleCatalog for built-in style definitions.
    /// </summary>
    public class PresetManager
    {
        private static PresetManager? _instance;
        public static PresetManager Instance => _instance ?? (_instance = new PresetManager());

        private readonly Dictionary<string, KillIconPreset> _presetCache = new Dictionary<string, KillIconPreset>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> _audioCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Currently active preset key. Set to null/empty to use ConfigurationManager defaults.
        /// </summary>
        public string? ActivePresetKey { get; set; }

        public bool IsInitialized { get; private set; }

        public string PresetDirectory { get; set; } = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "plugins", "SimpleHitMarker", "Presets");

        /// <summary>
        /// Load / reload all presets from disk. Auto-generates defaults from StyleCatalog if empty.
        /// </summary>
        /// <remarks>
        /// Synchronous and main-thread only — it creates Texture2D and AudioClip objects.
        /// </remarks>
        public void LoadAllPresets()
        {
            _presetCache.Clear();
            _textureCache.Clear();
            _audioCache.Clear();

            if (!Directory.Exists(PresetDirectory))
                Directory.CreateDirectory(PresetDirectory);

            // 1. Generate defaults if no JSON files exist
            var jsonFiles = Directory.GetFiles(PresetDirectory, "*.json", SearchOption.AllDirectories);
            if (jsonFiles.Length == 0)
            {
                GenerateDefaultPresets();
                jsonFiles = Directory.GetFiles(PresetDirectory, "*.json", SearchOption.AllDirectories);
            }

            // 2. JSON configs
            foreach (var file in jsonFiles)
                LoadPresetConfig(file);

            // 3. Images (delegate to TextureLoader)
            var imageFiles = new List<string>();
            imageFiles.AddRange(Directory.GetFiles(PresetDirectory, "*.png", SearchOption.AllDirectories));
            imageFiles.AddRange(Directory.GetFiles(PresetDirectory, "*.jpg", SearchOption.AllDirectories));
            foreach (var file in imageFiles)
            {
                var tex = TextureLoader.LoadTextureFromFile(file);
                if (tex != null)
                {
                    string key = Path.GetFileNameWithoutExtension(file);
                    tex.name = key;
                    _textureCache[key] = tex;
                }
            }

            // 4. Audio (delegate to AudioLoader, which already uses reflection for cross-version compat)
            var audioFiles = new List<string>();
            audioFiles.AddRange(Directory.GetFiles(PresetDirectory, "*.wav", SearchOption.AllDirectories));
            audioFiles.AddRange(Directory.GetFiles(PresetDirectory, "*.ogg", SearchOption.AllDirectories));
            audioFiles.AddRange(Directory.GetFiles(PresetDirectory, "*.mp3", SearchOption.AllDirectories));
            // Must stay on the main thread: AudioLoader goes through UnityWebRequestMultimedia
            // and creates an AudioClip, and setting clip.name touches a UnityEngine.Object.
            // Doing any of that on a thread-pool thread aborts the process natively.
            foreach (var file in audioFiles)
            {
                var clip = AudioLoader.LoadAudioFromFile(file);
                if (clip != null)
                {
                    string key = Path.GetFileNameWithoutExtension(file);
                    clip.name = key;
                    _audioCache[key] = clip;
                }
            }

            IsInitialized = true;
            Debug.Log($"[PresetManager] Loaded {_presetCache.Count} configs, {_textureCache.Count} textures, {_audioCache.Count} audio clips.");
        }

        // ========================================================
        // Default generation
        // ========================================================

        public void GenerateDefaultPresets()
        {
            try
            {
                foreach (var style in StyleCatalog.GetAllStyles())
                {
                    var preset = new KillIconPreset();
                    StyleCatalog.ApplyStyleToPreset(style, preset);

                    string json = JsonConvert.SerializeObject(preset, Formatting.Indented);
                    string filePath = Path.Combine(PresetDirectory, $"{style.StyleId}.json");
                    File.WriteAllText(filePath, json);
                    Debug.Log($"[PresetManager] Generated default preset: {style.StyleId} ({style.DisplayName})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PresetManager] Error generating default presets: {ex.Message}");
            }
        }

        // ========================================================
        // Loaders
        // ========================================================

        private void LoadPresetConfig(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var preset = JsonConvert.DeserializeObject<KillIconPreset>(json);
                if (preset != null)
                {
                    string key = Path.GetFileNameWithoutExtension(filePath);
                    _presetCache[key] = preset;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PresetManager] Error loading config {filePath}: {ex.Message}");
            }
        }

        // ========================================================
        // Getters
        // ========================================================

        public KillIconPreset? GetActivePreset()
        {
            if (string.IsNullOrEmpty(ActivePresetKey)) return null;
            return GetPreset(ActivePresetKey);
        }

        public KillIconPreset? GetPreset(string key)
        {
            if (_presetCache.TryGetValue(key, out var preset))
                return preset;

            var style = StyleCatalog.GetStyle(key);
            if (style != null && _presetCache.TryGetValue(style.StyleId, out var exactPreset))
                return exactPreset;

            return null;
        }

        public Texture2D? GetTexture(string key)
        {
            _textureCache.TryGetValue(key, out var tex);
            return tex;
        }

        public AudioClip? GetAudioClip(string key)
        {
            _audioCache.TryGetValue(key, out var clip);
            return clip;
        }

        public IEnumerable<string> GetAllPresetKeys() => _presetCache.Keys;
    }
}
