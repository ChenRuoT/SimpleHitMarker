using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using BepInEx.Logging;

namespace SimpleHitMarker
{
    public class AudioManager
    {
        private readonly ConfigurationManager _config;
        private readonly ManualLogSource _log;

        private AudioClip _hitSoundClip;
        private AudioClip _headshotHitSoundClip;
        private AudioClip _killSoundClip;
        private AudioClip _headshotKillSoundClip;

        private AudioSource _audioSource;
        private GameObject _audioSourceGameObject;

        public AudioManager(ConfigurationManager config, ManualLogSource log)
        {
            _config = config;
            _log = log;
            InitializeAudio();
        }

        private void InitializeAudio()
        {
            try
            {
                _audioSourceGameObject = new GameObject("SimpleHitMarker_AudioSource");
                UnityEngine.Object.DontDestroyOnLoad(_audioSourceGameObject);
                _audioSource = _audioSourceGameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
                _audioSource.volume = 1f;

                _log?.LogInfo($"[SimpleHitMarker] AudioSource created: {_audioSource != null}, GameObject: {_audioSourceGameObject.name}");

                LoadHitSounds();

                _log?.LogInfo($"[SimpleHitMarker] Audio clips loaded - hit: {_hitSoundClip != null}, headshotHit: {_headshotHitSoundClip != null}, kill: {_killSoundClip != null}, headshotKill: {_headshotKillSoundClip != null}");
                _log?.LogInfo("[SimpleHitMarker] Audio system initialized");
            }
            catch (Exception ex)
            {
                _log?.LogError($"[SimpleHitMarker] Audio initialization error: {ex}");
            }
        }

        public void PlayHitSound(bool isHeadshot)
        {
            if (_config.EnableHitSound.Value == false)
            {
                return;
            }

            AudioSource source = GetOrCreateAudioSource();
            if (source == null) return;

            try
            {
                if (!IsAudioClipValid(_hitSoundClip) || !IsAudioClipValid(_headshotHitSoundClip))
                {
                    ReloadAudioClips();
                }

                AudioClip clipToPlay = null;

                if (isHeadshot && IsAudioClipValid(_headshotHitSoundClip))
                {
                    clipToPlay = _headshotHitSoundClip;
                }
                else if (IsAudioClipValid(_hitSoundClip))
                {
                    clipToPlay = _hitSoundClip;
                }
                else
                {
                    return;
                }

                if (clipToPlay != null)
                {
                    float volume = _config.HitSoundVolume.Value;
                    source.PlayOneShot(clipToPlay, volume);
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[SimpleHitMarker] Error playing hit sound: {ex}");
            }
        }

        public void PlayKillSound(bool isHeadshot)
        {
            if (_config.EnableKillSound.Value == false)
            {
                return;
            }

            AudioSource source = GetOrCreateAudioSource();
            if (source == null) return;

            try
            {
                if (!IsAudioClipValid(_killSoundClip) || !IsAudioClipValid(_headshotKillSoundClip))
                {
                    ReloadAudioClips();
                }

                AudioClip clipToPlay = null;

                if (isHeadshot && IsAudioClipValid(_headshotKillSoundClip))
                {
                    clipToPlay = _headshotKillSoundClip;
                }
                else if (IsAudioClipValid(_killSoundClip))
                {
                    clipToPlay = _killSoundClip;
                }
                else if (isHeadshot && IsAudioClipValid(_headshotHitSoundClip))
                {
                    clipToPlay = _headshotHitSoundClip;
                }
                else if (IsAudioClipValid(_hitSoundClip))
                {
                    clipToPlay = _hitSoundClip;
                }
                else
                {
                    return;
                }

                if (clipToPlay != null)
                {
                    float volume = _config.KillSoundVolume.Value;
                    source.PlayOneShot(clipToPlay, volume);
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[SimpleHitMarker] Error playing kill sound: {ex}");
            }
        }

        private AudioSource GetOrCreateAudioSource()
        {
            if (_audioSource != null && _audioSource.gameObject != null)
            {
                return _audioSource;
            }

            if (_audioSourceGameObject != null)
            {
                _audioSource = _audioSourceGameObject.GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = _audioSourceGameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 0f;
                    _audioSource.volume = 1f;
                }
                return _audioSource;
            }

            try
            {
                _audioSourceGameObject = new GameObject("SimpleHitMarker_AudioSource");
                UnityEngine.Object.DontDestroyOnLoad(_audioSourceGameObject);
                _audioSource = _audioSourceGameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
                _audioSource.volume = 1f;

                return _audioSource;
            }
            catch (Exception ex)
            {
                _log?.LogError($"[SimpleHitMarker] Failed to recreate AudioSource: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Proactively check if the AudioSource and AudioClips are still valid, and recreate/reload if necessary.
        /// This should be called from Update or a Scene transition to ensure the audio system is always ready.
        /// </summary>
        public void CheckAndRestoreSource()
        {
            // 1. Restore AudioSource component if lost
            if (_audioSource == null || _audioSource.gameObject == null)
            {
                _log?.LogDebug("[SimpleHitMarker] AudioSource missing, restoring...");
                GetOrCreateAudioSource();
            }

            // 2. Proactively check if clips are still in memory
            // If any critical clip is invalid, reload all of them
            if (!IsAudioClipValid(_hitSoundClip) ||
                !IsAudioClipValid(_headshotHitSoundClip) ||
                !IsAudioClipValid(_killSoundClip) ||
                !IsAudioClipValid(_headshotKillSoundClip))
            {
                _log?.LogInfo("[SimpleHitMarker] Audio clips found invalid in background check, reloading proactively...");
                ReloadAudioClips();
            }
        }

        private void ReloadAudioClips()
        {
            LoadHitSounds();
        }

        private void LoadHitSounds()
        {
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string soundDir = Path.Combine(assemblyDir, "SimpleHitMarker");

                AudioClip LoadOgg(string fileName, string logicalName)
                {
                    string path = Path.Combine(soundDir, fileName);
                    if (!File.Exists(path))
                    {
                        return null;
                    }

                    var clip = AudioLoader.LoadAudioFromFile(path);
                    if (clip != null)
                    {
                        clip.hideFlags = HideFlags.DontSave;
                    }
                    return clip;
                }

                if (!IsAudioClipValid(_hitSoundClip)) _hitSoundClip = LoadOgg("hit.ogg", "hit.ogg");
                if (!IsAudioClipValid(_headshotHitSoundClip)) _headshotHitSoundClip = LoadOgg("headshot_hit.ogg", "headshot_hit.ogg");
                if (!IsAudioClipValid(_killSoundClip)) _killSoundClip = LoadOgg("kill.ogg", "kill.ogg");
                if (!IsAudioClipValid(_headshotKillSoundClip)) _headshotKillSoundClip = LoadOgg("headshot_kill.ogg", "headshot_kill.ogg");

                if (_hitSoundClip == null) _log?.LogWarning("[SimpleHitMarker] hit.ogg not found.");
                if (_killSoundClip == null) _log?.LogWarning("[SimpleHitMarker] kill.ogg not found.");
            }
            catch (Exception ex)
            {
                _log?.LogError($"[SimpleHitMarker] Error loading hit sounds: {ex}");
            }
        }

        private static bool IsAudioClipValid(AudioClip clip)
        {
            if (clip == null) return false;
            try
            {
                if (string.IsNullOrEmpty(clip.name) && clip.length <= 0) return false;
                var samples = clip.samples;
                return samples > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
