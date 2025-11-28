using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using static HairRenderer;
using SimpleHitMarker.KillFeed;
using SimpleHitmarker.KillPatch;

namespace SimpleHitMarker
{
    [BepInPlugin("com.shiunaya.simplehitmarker", "SimpleHitMarker", "0.1.1")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;
        public static bool hitDetected = false;
        public static Vector3 hitWorldPoint;
        public static float hitTime = 0f;
        public static float hitDamage = 0f; // store last hit damage

        // 暴露静态日志，便于补丁中可靠输出到 BepInEx 控制台/日志
        public static BepInEx.Logging.ManualLogSource Log;

        //贴图与动画
        public static Texture2D hitTexture;
        
        // 击杀提示系统
        public static KillFeedUI KillFeedUI { get; private set; }
        public static Texture2D skullTexture;
        public static Texture2D redSkullTexture;

        private const int MaxPmcRankIconLevel = 79;
        private static readonly Dictionary<int, Texture2D> PmcRankIcons = new Dictionary<int, Texture2D>();
        private static readonly int[] PmcRankTierStarts =
        {
            1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75
        };
        private static readonly HashSet<string> PmcBotTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USEC",
            "BEAR"
        };
        
        // 音频系统
        public static AudioClip hitSoundClip;
        public static AudioClip headshotHitSoundClip;
        public static AudioClip killSoundClip;
        public static AudioClip headshotKillSoundClip;
        private static AudioSource audioSource;
        private static GameObject audioSourceGameObject;

        // ============ 配置项 ============
        // Hit Marker 配置
        public static ConfigEntry<float> HitDuration { get; set; }
        public static ConfigEntry<float> HitBaseSize { get; set; }
        public static ConfigEntry<Vector2> HitMarkerCenterOffset { get; set; }
        public static ConfigEntry<float> HitMarkerAnimationScale { get; set; }
        
        // Hit Marker 伤害文字配置
        public static ConfigEntry<bool> ShowDamageText { get; set; }
        public static ConfigEntry<float> DamageTextPadding { get; set; }
        public static ConfigEntry<int> DamageTextMinSize { get; set; }
        public static ConfigEntry<int> DamageTextMaxSize { get; set; }
        public static ConfigEntry<int> DamageTextSize { get; set; }
        public static ConfigEntry<Color> DamageTextColor { get; set; }
        public static ConfigEntry<Color> DamageTextOutlineColor { get; set; }
        public static ConfigEntry<Color> DamageTextHeadshotOutlineColor { get; set; }
        public static ConfigEntry<float> DamageMultiTextPadding { get; set; }
        public static ConfigEntry<float> DamageTextOutlineOpacity { get; set; }
        public static ConfigEntry<float> DamageTextOutlineThickness { get; set; }
        
        // Kill Feed 位置配置
        public static ConfigEntry<float> KillFeedHorizontalOffset { get; set; }
        public static ConfigEntry<float> KillFeedVerticalOffset { get; set; }
        public static ConfigEntry<float> KillFeedLineSpacing { get; set; }
        public static ConfigEntry<float> KillFeedBlockWidth { get; set; }
        public static ConfigEntry<float> KillFeedExperienceHorizontalOffset { get; set; }
        
        // Kill Feed 时间配置
        public static ConfigEntry<float> KillFeedDuration { get; set; }
        public static ConfigEntry<float> SkullDisplayDuration { get; set; }
        public static ConfigEntry<float> SkullFadeDuration { get; set; }
        public static ConfigEntry<float> StreakWindow { get; set; }
        
        // Kill Feed 骷髅头配置
        public static ConfigEntry<float> SkullSize { get; set; }
        public static ConfigEntry<float> SkullSpacing { get; set; }
        public static ConfigEntry<float> SkullAnimationSpeed { get; set; }
        public static ConfigEntry<float> SkullPushAnimationSpeed { get; set; }
        
        // Kill Feed 字体配置
        public static ConfigEntry<int> FontSizeFaction { get; set; }
        public static ConfigEntry<int> FontSizeExperience { get; set; }
        public static ConfigEntry<int> FontSizePlayerName { get; set; }
        public static ConfigEntry<int> FontSizeKillDetails { get; set; }
        
        // Kill Feed 颜色配置
        public static ConfigEntry<Color> ColorFaction { get; set; }
        public static ConfigEntry<Color> ColorExperience { get; set; }
        public static ConfigEntry<Color> ColorPlayerName { get; set; }
        public static ConfigEntry<Color> ColorKillDetails { get; set; }

        // Per-bot-type colors
        public static ConfigEntry<Color> ColorUSEC { get; set; }
        public static ConfigEntry<Color> ColorBEAR { get; set; }
        public static ConfigEntry<Color> ColorSCAV { get; set; }
        public static ConfigEntry<Color> ColorBOSS { get; set; }
        public static ConfigEntry<Color> ColorFollower { get; set; }
        public static ConfigEntry<Color> ColorRAIDER { get; set; }
        public static ConfigEntry<Color> ColorRouges { get; set; }
        public static ConfigEntry<Color> ColorSectant { get; set; }
        
        // Kill Feed 其他配置
        public static ConfigEntry<float> FactionIconSize { get; set; }
        public static ConfigEntry<float> FactionIconVerticalOffset { get; set; }
        public static ConfigEntry<float> ExperienceTextWidth { get; set; }

        // 玩家名字描边配置
        public static ConfigEntry<float> NameOutlineOpacity { get; set; }
        public static ConfigEntry<float> NameOutlineThickness { get; set; }

        // Debug 配置
        public static ConfigEntry<KeyboardShortcut> DebugTriggerKey { get; set; }
        
        // 音频配置
        public static ConfigEntry<bool> EnableHitSound { get; set; }
        public static ConfigEntry<bool> EnableKillSound { get; set; }
        public static ConfigEntry<float> HitSoundVolume { get; set; }
        public static ConfigEntry<float> KillSoundVolume { get; set; }

        private static readonly object DamageEntriesLock = new object();
        private static readonly List<DamageDisplayEntry> DamageEntries = new List<DamageDisplayEntry>();
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

        private class DamageDisplayEntry
        {
            public float Damage;
            public bool IsHeadshot;
            public float Timestamp;
        }

        private static readonly System.Random DebugRandom = new System.Random();
        private static readonly string[] DebugNames = { "Tigris", "Northwind", "KappaFox", "Windrunner", "NightOwl", "Skyline" };
        private static readonly string[] DebugKillMethods = { "M4A1", "AK-105", "MP7A2", "SR-25", "SV-98", "UMP-45", "MK17" };
        private static readonly string[] DebugFactions = { "USEC", "BEAR", "Scav" };
        private static readonly EBodyPart[] DebugBodyParts = { EBodyPart.Head, EBodyPart.Chest, EBodyPart.Stomach, EBodyPart.LeftArm, EBodyPart.RightArm, EBodyPart.LeftLeg, EBodyPart.RightLeg };

        private void Start()
        {
            // 初始化所有配置项
            InitializeConfigs();
        }

        private void Awake()
        {
            //先设置静态 Log，再打补丁
            Log = Logger;

            // 使用 TextureLoader 从插件子目录加载 hit.png
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string hitPngpath = Path.Combine(assemblyDir, "SimpleHitMarker", "hit.png");
                Log.LogInfo($"[SimpleHitMarker] Looking for hit texture at: {hitPngpath}");
                hitTexture = TextureLoader.LoadTextureFromFile(hitPngpath);
                if (hitTexture == null)
                {
                    //也尝试 DLL 同目录
                    string alt = Path.Combine(assemblyDir, "hit.png");
                    Log.LogInfo($"[SimpleHitMarker] Trying alternate path: {alt}");
                    hitTexture = TextureLoader.LoadTextureFromFile(alt);
                }

                if (hitTexture == null)
                {
                    Log.LogWarning("[SimpleHitMarker] hit.png not found in plugin folder or SimpleHitMarker subfolder. Using simple X fallback.");
                }
            }
            catch (Exception ex)
            {
                try { Log.LogError($"[SimpleHitMarker] Texture load error: {ex}"); } catch { }
            }
            
            // 加载骷髅头纹理
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                
                // 加载普通骷髅头
                string skullPath = Path.Combine(assemblyDir, "SimpleHitMarker", "skull.png");
                skullTexture = TextureLoader.LoadTextureFromFile(skullPath);
                if (skullTexture == null)
                {
                    Log.LogWarning("[SimpleHitMarker] skull.png not found. Kill feed skull will not display.");
                }
                
                // 加载红色骷髅头（爆头）
                string redSkullPath = Path.Combine(assemblyDir, "SimpleHitMarker", "skull_head.png");
                redSkullTexture = TextureLoader.LoadTextureFromFile(redSkullPath);
                if (redSkullTexture == null)
                {
                    // 如果没有红色骷髅头，使用普通骷髅头
                    redSkullTexture = skullTexture;
                    Log.LogInfo("[SimpleHitMarker] skull_head.png not found. Using regular skull for headshots.");
                }
            }
            catch (Exception ex)
            {
                try { Log.LogError($"[SimpleHitMarker] Skull texture load error: {ex}"); } catch { }
            }

            LoadPmcRankIcons();
            
            // 初始化音频系统
            InitializeAudio();
            
            harmony = new Harmony("com.shiunaya.simplehitmarker");
            harmony.PatchAll();
            
            Log.LogInfo("SimpleHitMarker Plugin is loaded!");
        }
        
        /// <summary>
        /// 初始化音频系统
        /// </summary>
        private void InitializeAudio()
        {
            try
            {
                // 创建 AudioSource GameObject
                audioSourceGameObject = new GameObject("SimpleHitMarker_AudioSource");
                UnityEngine.Object.DontDestroyOnLoad(audioSourceGameObject);
                audioSource = audioSourceGameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D 音效
                audioSource.volume = 1f;
                
                Log.LogInfo($"[SimpleHitMarker] AudioSource created: {audioSource != null}, GameObject: {audioSourceGameObject.name}");
                
                // 加载音效文件
                LoadHitSounds();
                
                // 验证加载的音频剪辑
                Log.LogInfo($"[SimpleHitMarker] Audio clips loaded - hit: {hitSoundClip != null}, headshotHit: {headshotHitSoundClip != null}, kill: {killSoundClip != null}, headshotKill: {headshotKillSoundClip != null}");
                
                Log.LogInfo("[SimpleHitMarker] Audio system initialized");
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleHitMarker] Audio initialization error: {ex}");
            }
        }

        private void LoadPmcRankIcons()
        {
            try
            {
                PmcRankIcons.Clear();

                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string[] candidateFolders =
                {
                    Path.Combine(assemblyDir, "SimpleHitMarker", "RankIcons"),
                    Path.Combine(assemblyDir, "RankIcons")
                };

                var existingFolders = new List<string>();
                foreach (var folder in candidateFolders)
                {
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        existingFolders.Add(folder);
                    }
                }

                if (existingFolders.Count == 0)
                {
                    Log?.LogWarning("[SimpleHitMarker] PMC rank icon folder not found. Rank icons will be hidden.");
                    return;
                }

                foreach (int tier in PmcRankTierStarts)
                {
                    Texture2D iconTexture = null;
                    string fileName = $"Rank{tier}.png";

                    foreach (var folder in existingFolders)
                    {
                        string candidatePath = Path.Combine(folder, fileName);
                        if (!File.Exists(candidatePath))
                        {
                            continue;
                        }

                        iconTexture = TextureLoader.LoadTextureFromFile(candidatePath);
                        if (iconTexture != null)
                        {
                            break;
                        }
                    }

                    if (iconTexture != null)
                    {
                        PmcRankIcons[tier] = iconTexture;
                    }
                }

                Log?.LogInfo($"[SimpleHitMarker] PMC rank icons loaded: {PmcRankIcons.Count}/{PmcRankTierStarts.Length}");
            }
            catch (Exception ex)
            {
                Log?.LogError($"[SimpleHitMarker] Failed to load PMC rank icons: {ex}");
            }
        }

        internal static Texture2D GetPmcRankIcon(string botType, int playerLevel)
        {
            if (!IsPmcBotType(botType) || playerLevel <= 0)
            {
                return null;
            }

            int tierKey = ResolvePmcRankTierStart(playerLevel);
            if (tierKey <= 0)
            {
                return null;
            }

            if (PmcRankIcons.TryGetValue(tierKey, out var icon))
            {
                return icon;
            }

            return null;
        }

        private static bool IsPmcBotType(string botType)
        {
            return !string.IsNullOrWhiteSpace(botType) && PmcBotTypes.Contains(botType);
        }

        private static int ResolvePmcRankTierStart(int playerLevel)
        {
            if (playerLevel <= 0)
            {
                return -1;
            }

            if (playerLevel <= 4)
            {
                return 1;
            }

            int clamped = Mathf.Clamp(playerLevel, 5, MaxPmcRankIconLevel);
            int steps = (clamped - 5) / 5;
            int tierStart = 5 + (steps * 5);

            return Mathf.Clamp(tierStart, 5, 75);
        }
        
        /// <summary>
        /// 检查 AudioClip 是否有效
        /// </summary>
        private static bool IsAudioClipValid(AudioClip clip)
        {
            if (clip == null)
            {
                return false;
            }
            
            try
            {
                // 检查 AudioClip 是否有名称和长度（基本有效性检查）
                if (string.IsNullOrEmpty(clip.name) && clip.length <= 0)
                {
                    return false;
                }
                
                // 尝试访问 samples 属性来验证 AudioClip 是否真的加载了数据
                // 如果 AudioClip 失效，访问这些属性可能会抛出异常
                var samples = clip.samples;
                return samples > 0;
            }
            catch
            {
                // 如果访问属性时出错，说明 AudioClip 已失效
                return false;
            }
        }
        
        /// <summary>
        /// 重新加载音频剪辑（如果无效）
        /// </summary>
        private static void ReloadAudioClipsIfNeeded()
        {
            bool needReload = false;
            
            // 检查所有音频剪辑是否有效
            if (!IsAudioClipValid(hitSoundClip))
            {
                Log.LogWarning("[SimpleHitMarker] hitSoundClip is invalid, will reload");
                hitSoundClip = null;
                needReload = true;
            }
            
            if (!IsAudioClipValid(headshotHitSoundClip))
            {
                Log.LogWarning("[SimpleHitMarker] headshotHitSoundClip is invalid, will reload");
                headshotHitSoundClip = null;
                needReload = true;
            }
            
            if (!IsAudioClipValid(killSoundClip))
            {
                Log.LogWarning("[SimpleHitMarker] killSoundClip is invalid, will reload");
                killSoundClip = null;
                needReload = true;
            }
            
            if (!IsAudioClipValid(headshotKillSoundClip))
            {
                Log.LogWarning("[SimpleHitMarker] headshotKillSoundClip is invalid, will reload");
                headshotKillSoundClip = null;
                needReload = true;
            }
            
            if (needReload)
            {
                Log.LogInfo("[SimpleHitMarker] Reloading audio clips...");
                // 调用静态方法重新加载（需要创建临时实例或使用其他方式）
                // 由于 LoadHitSounds 是实例方法，我们需要另一种方式
                // 暂时先记录，实际重新加载会在下次需要时触发
            }
        }
        
        /// <summary>
        /// 静态方法：重新加载音频剪辑
        /// </summary>
        private static void ReloadAudioClipsStatic()
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
                        Log.LogInfo($"[SimpleHitMarker] Reloaded {logicalName}: {clip.name}, length={clip.length}");
                    }
                    return clip;
                }

                if (!IsAudioClipValid(hitSoundClip))
                {
                    hitSoundClip = LoadOgg("hit.ogg", "hit.ogg");
                }

                if (!IsAudioClipValid(headshotHitSoundClip))
                {
                    headshotHitSoundClip = LoadOgg("headshot_hit.ogg", "headshot_hit.ogg");
                }

                if (!IsAudioClipValid(killSoundClip))
                {
                    killSoundClip = LoadOgg("kill.ogg", "kill.ogg");
                }

                if (!IsAudioClipValid(headshotKillSoundClip))
                {
                    headshotKillSoundClip = LoadOgg("headshot_kill.ogg", "headshot_kill.ogg");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleHitMarker] Error reloading audio clips: {ex}");
            }
        }
        
        /// <summary>
        /// 获取或创建 AudioSource（如果丢失则重新创建）
        /// </summary>
        private static AudioSource GetOrCreateAudioSource()
        {
            // 如果 AudioSource 存在且有效，直接返回
            if (audioSource != null && audioSource.gameObject != null)
            {
                return audioSource;
            }
            
            // 如果 GameObject 存在但 AudioSource 丢失，重新添加组件
            if (audioSourceGameObject != null)
            {
                Log.LogWarning("[SimpleHitMarker] AudioSource component lost, re-adding...");
                audioSource = audioSourceGameObject.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = audioSourceGameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                    audioSource.volume = 1f;
                }
                Log.LogInfo("[SimpleHitMarker] AudioSource recreated successfully");
                
                // 重新加载音频剪辑（如果无效）
                ReloadAudioClipsStatic();
                
                return audioSource;
            }
            
            // 如果 GameObject 也不存在，重新创建整个系统
            Log.LogWarning("[SimpleHitMarker] AudioSource GameObject lost, recreating...");
            try
            {
                audioSourceGameObject = new GameObject("SimpleHitMarker_AudioSource");
                UnityEngine.Object.DontDestroyOnLoad(audioSourceGameObject);
                audioSource = audioSourceGameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
                audioSource.volume = 1f;
                Log.LogInfo("[SimpleHitMarker] AudioSource GameObject and component recreated successfully");
                
                // 重新加载音频剪辑（如果无效）
                ReloadAudioClipsStatic();
                
                return audioSource;
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleHitMarker] Failed to recreate AudioSource: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// 加载命中音效文件
        /// </summary>
        private void LoadHitSounds()
        {
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string soundDir = Path.Combine(assemblyDir, "SimpleHitMarker");

                Log.LogInfo($"[SimpleHitMarker] LoadHitSounds: assemblyDir={assemblyDir}");
                Log.LogInfo($"[SimpleHitMarker] LoadHitSounds: soundDir={soundDir}");

                AudioClip LoadOgg(string fileName, string logicalName)
                {
                    string path = Path.Combine(soundDir, fileName);
                    try
                    {
                        bool exists = File.Exists(path);
                        Log.LogInfo($"[SimpleHitMarker] Checking {logicalName} path: {path} exists={exists}");
                        if (!exists)
                        {
                            return null;
                        }

                        try
                        {
                            var bytes = File.ReadAllBytes(path);
                            Log.LogInfo($"[SimpleHitMarker] {logicalName} readable, size={bytes.Length} bytes");
                        }
                        catch (Exception ex)
                        {
                            Log.LogError($"[SimpleHitMarker] {logicalName} read error: {ex}");
                            return null;
                        }

                        var clip = AudioLoader.LoadAudioFromFile(path);
                        if (clip == null)
                        {
                            Log.LogWarning($"[SimpleHitMarker] {logicalName} failed to load via AudioLoader: {path}");
                        }
                        else
                        {
                            Log.LogInfo($"[SimpleHitMarker] {logicalName} loaded: {path}");
                        }

                        return clip;
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"[SimpleHitMarker] Error checking {logicalName}: {ex}");
                        return null;
                    }
                }

                //仅支持 .ogg 格式
                hitSoundClip = LoadOgg("hit.ogg", "hit.ogg");
                if (hitSoundClip == null)
                {
                    Log.LogWarning("[SimpleHitMarker] hit.ogg not found. Hit sound will not play.");
                }

                headshotHitSoundClip = LoadOgg("headshot_hit.ogg", "headshot_hit.ogg");
                if (headshotHitSoundClip == null)
                {
                    Log.LogInfo("[SimpleHitMarker] headshot_hit.ogg not found. Using regular hit sound for headshots.");
                }

                killSoundClip = LoadOgg("kill.ogg", "kill.ogg");
                if (killSoundClip == null)
                {
                    Log.LogWarning("[SimpleHitMarker] kill.ogg not found. Kill sound will not play.");
                }

                headshotKillSoundClip = LoadOgg("headshot_kill.ogg", "headshot_kill.ogg");
                if (headshotKillSoundClip == null)
                {
                    Log.LogInfo("[SimpleHitMarker] headshot_kill.ogg not found. Using regular kill sound for headshot kills.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleHitMarker] Error loading hit sounds: {ex}");
            }
        }

        /// <summary>
        /// 初始化所有配置项
        /// </summary>
        private void InitializeConfigs()
        {
            // ============ Hit Marker 配置 ============
            HitDuration = Config.Bind<float>(
                "Hit Marker",
                "显示时长",
                0.8680753f,
                new ConfigDescription(
                    "击中标记显示的时间长度（秒）",
                    new AcceptableValueRange<float>(0.1f, 5.0f),
                    new ConfigurationManagerAttributes { Order = 100 }
                )
            );

            HitBaseSize = Config.Bind<float>(
                "Hit Marker",
                "基础大小",
                71f,
                new ConfigDescription(
                    "击中标记的基础像素大小",
                    new AcceptableValueRange<float>(32f, 512f),
                    new ConfigurationManagerAttributes { Order = 90 }
                )
            );

            HitMarkerCenterOffset = Config.Bind<Vector2>(
                "Hit Marker",
                "中心偏移",
                new Vector2(0f, 5f),
                new ConfigDescription(
                    "击中标记相对屏幕中心的偏移（像素）",
                    null,
                    new ConfigurationManagerAttributes { Order = 80, IsAdvanced = true }
                )
            );

            HitMarkerAnimationScale = Config.Bind<float>(
                "Hit Marker",
                "动画缩放",
                1f,
                new ConfigDescription(
                    "击中标记动画的最大缩放倍数",
                    new AcceptableValueRange<float>(1.0f, 2.0f),
                    new ConfigurationManagerAttributes { Order = 70, IsAdvanced = true }
                )
            );

            // ============ Hit Marker 伤害文字配置 ============
            ShowDamageText = Config.Bind<bool>(
                "Hit Marker",
                "显示伤害文字",
                true,
                new ConfigDescription(
                    "是否在击中标记旁显示伤害数值",
                    null,
                    new ConfigurationManagerAttributes { Order = 60 }
                )
            );

            DamageTextPadding = Config.Bind<float>(
                "Hit Marker",
                "伤害文字间距",
                8f,
                new ConfigDescription(
                    "伤害文字与击中标记之间的间距（像素）",
                    new AcceptableValueRange<float>(0f, 50f),
                    new ConfigurationManagerAttributes { Order = 50, IsAdvanced = true }
                )
            );

            DamageTextMinSize = Config.Bind<int>(
                "Hit Marker",
                "伤害文字最小字号",
                15,
                new ConfigDescription(
                    "伤害文字的最小字体大小",
                    new AcceptableValueRange<int>(8, 24),
                    new ConfigurationManagerAttributes { Order = 40, IsAdvanced = true }
                )
            );

            DamageTextMaxSize = Config.Bind<int>(
                "Hit Marker",
                "伤害文字最大字号",
                73,
                new ConfigDescription(
                    "伤害文字的最大字体大小",
                    new AcceptableValueRange<int>(24, 96),
                    new ConfigurationManagerAttributes { Order = 30, IsAdvanced = true }
                )
            );

            DamageTextSize = Config.Bind<int>(
                "Hit Marker",
                "伤害文字字号",
                21,
                new ConfigDescription(
                    "伤害文字的固定字体大小",
                    new AcceptableValueRange<int>(8, 96),
                    new ConfigurationManagerAttributes { Order = 25 }
                )
            );

            DamageTextColor = Config.Bind<Color>(
                "Hit Marker",
                "伤害文字颜色",
                Color.white,
                new ConfigDescription(
                    "伤害文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 20, IsAdvanced = true }
                )
            );

            DamageTextOutlineColor = Config.Bind<Color>(
                "Hit Marker",
                "伤害文字描边颜色",
                Color.white,
                new ConfigDescription(
                    "伤害文字描边的基础颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 19, IsAdvanced = true }
                )
            );

            DamageTextHeadshotOutlineColor = Config.Bind<Color>(
                "Hit Marker",
                "爆头伤害描边颜色",
                Color.red,
                new ConfigDescription(
                    "爆头伤害文字描边颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 18, IsAdvanced = true }
                )
            );

            DamageMultiTextPadding = Config.Bind<float>(
                "Hit Marker",
                "多段伤害间距",
                12f,
                new ConfigDescription(
                    "同一显示窗口内多段伤害数字之间的间距（像素）",
                    new AcceptableValueRange<float>(0f, 100f),
                    new ConfigurationManagerAttributes { Order = 17, IsAdvanced = true }
                )
            );

            DamageTextOutlineOpacity = Config.Bind<float>(
                "Hit Marker",
                "描边不透明度",
                0.2535212f,
                new ConfigDescription(
                    "伤害文字描边颜色的不透明度（0-1）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 16, IsAdvanced = true }
                )
            );

            DamageTextOutlineThickness = Config.Bind<float>(
                "Hit Marker",
                "描边厚度",
                1.4f,
                new ConfigDescription(
                    "伤害文字描边的像素厚度",
                    new AcceptableValueRange<float>(0.5f, 10f),
                    new ConfigurationManagerAttributes { Order = 15, IsAdvanced = true }
                )
            );

            // ============ Kill Feed 位置配置 ============
            KillFeedHorizontalOffset = Config.Bind<float>(
                "Kill Feed",
                "水平中心偏移",
                107f,
                new ConfigDescription(
                    "击杀提示相对屏幕中心向左的偏移量（像素）。正值向左移动，负值向右移动。",
                    new AcceptableValueRange<float>(-2000f, 2000f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "位置" }
                )
            );

            KillFeedVerticalOffset = Config.Bind<float>(
                "Kill Feed",
                "垂直中心偏移",
                0f,
                new ConfigDescription(
                    "击杀提示相对屏幕垂直中心的偏移量（像素）。正值向下移动，负值向上移动。",
                    new AcceptableValueRange<float>(-2000f, 2000f),
                    new ConfigurationManagerAttributes { Order = 90, Category = "位置" }
                )
            );

            

            KillFeedLineSpacing = Config.Bind<float>(
                "Kill Feed",
                "行间距",
                0f,
                new ConfigDescription(
                    "每行之间的间距（像素）",
                    new AcceptableValueRange<float>(0f, 50f),
                    new ConfigurationManagerAttributes { Order = 70, Category = "位置" }
                )
            );

            KillFeedBlockWidth = Config.Bind<float>(
                "Kill Feed",
                "内容宽度",
                420f,
                new ConfigDescription(
                    "每行文字内容的最大宽度（像素），用于右对齐布局。",
                    new AcceptableValueRange<float>(100f, 1000f),
                    new ConfigurationManagerAttributes { Order = 65, Category = "位置" }
                )
            );

            KillFeedExperienceHorizontalOffset = Config.Bind<float>(
                "Kill Feed",
                "经验值水平偏移",
                35f,
                new ConfigDescription(
                    "经验值数值相对锚点向右的偏移量（像素)。正值表示向右偏移。",
                    new AcceptableValueRange<float>(-500f, 500f),
                    new ConfigurationManagerAttributes { Order = 60, Category = "位置" }
                )
            );

            // ============ Kill Feed 时间配置 ============
            KillFeedDuration = Config.Bind<float>(
                "Kill Feed",
                "显示时长",
                6f,
                new ConfigDescription(
                    "每个击杀提示显示的时间长度（秒）",
                    new AcceptableValueRange<float>(1f, 30f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "时间" }
                )
            );

            SkullDisplayDuration = Config.Bind<float>(
                "Kill Feed",
                "骷髅头显示时长",
                2f,
                new ConfigDescription(
                    "骷髅头图标显示的时间长度（秒）",
                    new AcceptableValueRange<float>(0.5f, 10f),
                    new ConfigurationManagerAttributes { Order = 90, Category = "时间" }
                )
            );

            SkullFadeDuration = Config.Bind<float>(
                "Kill Feed",
                "骷髅头淡出时长",
                0.3f,
                new ConfigDescription(
                    "骷髅头淡出动画的时间长度（秒）",
                    new AcceptableValueRange<float>(0.1f, 2f),
                    new ConfigurationManagerAttributes { Order = 80, Category = "时间" }
                )
            );

            StreakWindow = Config.Bind<float>(
                "Kill Feed",
                "连杀时间窗口",
                10f,
                new ConfigDescription(
                    "在此时间内击杀算作连杀（秒）",
                    new AcceptableValueRange<float>(1f, 60f),
                    new ConfigurationManagerAttributes { Order = 70, Category = "时间" }
                )
            );

            // ============ Kill Feed 骷髅头配置 ============
            SkullSize = Config.Bind<float>(
                "Kill Feed",
                "骷髅头大小",
                40f,
                new ConfigDescription(
                    "骷髅头图标的像素大小",
                    new AcceptableValueRange<float>(16f, 256f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "骷髅头" }
                )
            );

            SkullSpacing = Config.Bind<float>(
                "Kill Feed",
                "骷髅头间距",
                20f,
                new ConfigDescription(
                    "连杀时骷髅头之间的间距（像素）",
                    new AcceptableValueRange<float>(20f, 200f),
                    new ConfigurationManagerAttributes { Order = 90, Category = "骷髅头" }
                )
            );

            SkullAnimationSpeed = Config.Bind<float>(
                "Kill Feed",
                "骷髅头动画速度",
                5f,
                new ConfigDescription(
                    "骷髅头位置动画的速度",
                    new AcceptableValueRange<float>(1f, 20f),
                    new ConfigurationManagerAttributes { Order = 80, Category = "骷髅头", IsAdvanced = true }
                )
            );

            SkullPushAnimationSpeed = Config.Bind<float>(
                "Kill Feed",
                "骷髅头推挤动画速度",
                7f,
                new ConfigDescription(
                    "连杀产生时旧骷髅头向左平移的速度。",
                    new AcceptableValueRange<float>(1f, 30f),
                    new ConfigurationManagerAttributes { Order = 75, Category = "骷髅头" }
                )
            );

            // ============ Kill Feed 字体配置 ============
            FontSizeFaction = Config.Bind<int>(
                "Kill Feed",
                "阵营文字字号",
                20,
                new ConfigDescription(
                    "阵营和等级文字的字体大小",
                    new AcceptableValueRange<int>(8, 48),
                    new ConfigurationManagerAttributes { Order = 100, Category = "字体" }
                )
            );

            FontSizeExperience = Config.Bind<int>(
                "Kill Feed",
                "经验值字号",
                20,
                new ConfigDescription(
                    "经验值文字的字体大小",
                    new AcceptableValueRange<int>(8, 48),
                    new ConfigurationManagerAttributes { Order = 90, Category = "字体" }
                )
            );

            FontSizePlayerName = Config.Bind<int>(
                "Kill Feed",
                "玩家名字号",
                18,
                new ConfigDescription(
                    "玩家名称文字的字体大小",
                    new AcceptableValueRange<int>(8, 48),
                    new ConfigurationManagerAttributes { Order = 80, Category = "字体" }
                )
            );

            FontSizeKillDetails = Config.Bind<int>(
                "Kill Feed",
                "击杀详情字号",
                20,
                new ConfigDescription(
                    "击杀详情（部位/距离/武器）文字的字体大小",
                    new AcceptableValueRange<int>(8, 48),
                    new ConfigurationManagerAttributes { Order = 70, Category = "字体" }
                )
            );

            // ============ Kill Feed 颜色配置 ============
            ColorFaction = Config.Bind<Color>(
                "Kill Feed",
                "阵营文字颜色",
                Color.white,
                new ConfigDescription(
                    "阵营和等级文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 100, Category = "颜色" }
                )
            );

            ColorExperience = Config.Bind<Color>(
                "Kill Feed",
                "经验值颜色",
                new Color32(0xFF, 0xCC, 0xFF, 0xFF),
                new ConfigDescription(
                    "经验值文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 90, Category = "颜色" }
                )
            );

            ColorPlayerName = Config.Bind<Color>(
                "Kill Feed",
                "玩家名称颜色",
                new Color32(0xFF, 0x00, 0x0F, 0xFF),
                new ConfigDescription(
                    "玩家名称文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 80, Category = "颜色" }
                )
            );

            ColorKillDetails = Config.Bind<Color>(
                "Kill Feed",
                "击杀详情颜色",
                new Color32(0xCC, 0xCC, 0xCC, 0xFF),
                new ConfigDescription(
                    "击杀详情文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 70, Category = "颜色" }
                )
            );
            // ============ Per-bot-type color defaults ============
            ColorUSEC = Config.Bind<Color>(
                "Kill Feed",
                "颜色_USEC",
                new Color(0.0f, 0.2f, 0.6f, 1f), // deep blue
                new ConfigDescription("USEC 文字颜色", null, new ConfigurationManagerAttributes { Order = 69, Category = "颜色" })
            );

            ColorBEAR = Config.Bind<Color>(
                "Kill Feed",
                "颜色_BEAR",
                new Color(1f, 0.55f, 0f, 1f), // orange
                new ConfigDescription("BEAR 文字颜色", null, new ConfigurationManagerAttributes { Order = 68, Category = "颜色" })
            );

            ColorSCAV = Config.Bind<Color>(
                "Kill Feed",
                "颜色_SCAV",
                new Color(1f, 0.9f, 0f, 1f), // yellow
                new ConfigDescription("SCAV 文字颜色", null, new ConfigurationManagerAttributes { Order = 67, Category = "颜色" })
            );

            ColorBOSS = Config.Bind<Color>(
                "Kill Feed",
                "颜色_BOSS",
                new Color(1f, 0f, 0f, 1f), // red
                new ConfigDescription("BOSS 文字颜色", null, new ConfigurationManagerAttributes { Order = 66, Category = "颜色" })
            );

            ColorFollower = Config.Bind<Color>(
                "Kill Feed",
                "颜色_Follower",
                new Color(0.9f, 0.2f, 0.2f, 1f), // slightly lighter red
                new ConfigDescription("Follower 文字颜色", null, new ConfigurationManagerAttributes { Order = 65, Category = "颜色" })
            );

            ColorRAIDER = Config.Bind<Color>(
                "Kill Feed",
                "颜色_RAIDER",
                new Color(1f, 0.4f, 0.7f, 1f), // pink
                new ConfigDescription("RAIDER 文字颜色", null, new ConfigurationManagerAttributes { Order = 64, Category = "颜色" })
            );

            ColorRouges = Config.Bind<Color>(
                "Kill Feed",
                "颜色_Rouges",
                new Color(0.2f, 0.2f, 0.2f, 1f), // dark gray
                new ConfigDescription("Rouges 文字颜色", null, new ConfigurationManagerAttributes { Order = 63, Category = "颜色" })
            );

            ColorSectant = Config.Bind<Color>(
                "Kill Feed",
                "颜色_Sectant",
                new Color(0f, 0.4f, 0f, 1f), // dark green
                new ConfigDescription("Sectant 文字颜色", null, new ConfigurationManagerAttributes { Order = 62, Category = "颜色" })
            );

            // ============ Kill Feed 其他配置 ============
            FactionIconSize = Config.Bind<float>(
                "Kill Feed",
                "阵营图标大小",
                86f,
                new ConfigDescription(
                    "阵营图标的大小（像素）",
                    new AcceptableValueRange<float>(8f, 128f),
                    new ConfigurationManagerAttributes { Order = 60, IsAdvanced = true }
                )
            );

            FactionIconVerticalOffset = Config.Bind<float>(
                "Kill Feed",
                "阵营图标垂直偏移",
                -30f,
                new ConfigDescription(
                    "阵营图标相对于原始布局的垂直偏移（像素），负值向上，正值向下。",
                    new AcceptableValueRange<float>(-200f, 200f),
                    new ConfigurationManagerAttributes { Order = 59, IsAdvanced = true }
                )
            );

            ExperienceTextWidth = Config.Bind<float>(
                "Kill Feed",
                "经验值文字宽度",
                180f,
                new ConfigDescription(
                    "经验值文字区域的宽度（像素）",
                    new AcceptableValueRange<float>(50f, 500f),
                    new ConfigurationManagerAttributes { Order = 50, IsAdvanced = true }
                )
            );

            // ============ 玩家名字描边配置 ============
            NameOutlineOpacity = Config.Bind<float>(
                "Kill Feed",
                "玩家名字描边不透明度",
                0.1549296f,
                new ConfigDescription(
                    "玩家名字描边颜色的不透明度（0-1）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 49, Category = "颜色" }
                )
            );

            NameOutlineThickness = Config.Bind<float>(
                "Kill Feed",
                "玩家名字描边厚度",
                1.1f,
                new ConfigDescription(
                    "玩家名字描边的像素厚度",
                    new AcceptableValueRange<float>(0.5f, 10f),
                    new ConfigurationManagerAttributes { Order = 48, Category = "颜色" }
                )
            );

            DebugTriggerKey = Config.Bind(
                "调试",
                "调试触发按键",
                new KeyboardShortcut(KeyCode.P),
                new ConfigDescription(
                    "按下该按键将立即生成一次随机击中反馈与击杀提示",
                    null,
                    new ConfigurationManagerAttributes { Order = 100, Category = "调试" }
                )
            );

            // ============ 音频配置 ============
            EnableHitSound = Config.Bind<bool>(
                "音频",
                "启用命中音效",
                true,
                new ConfigDescription(
                    "是否播放命中音效",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }
                )
            );

            EnableKillSound = Config.Bind<bool>(
                "音频",
                "启用击杀音效",
                true,
                new ConfigDescription(
                    "是否播放击杀音效",
                    null,
                    new ConfigurationManagerAttributes { Order = 90 }
                )
            );

            HitSoundVolume = Config.Bind<float>(
                "音频",
                "命中音效音量",
                0.5f,
                new ConfigDescription(
                    "命中音效的音量（0.0 - 1.0）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 80 }
                )
            );

            KillSoundVolume = Config.Bind<float>(
                "音频",
                "击杀音效音量",
                0.7f,
                new ConfigDescription(
                    "击杀音效的音量（0.0 - 1.0）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 70 }
                )
            );

            // 初始化击杀提示系统（需要在配置初始化后）
            KillFeedUI = new KillFeedUI
            {
                SkullTexture = skullTexture,
                RedSkullTexture = redSkullTexture
            };
            
            // 订阅击杀事件
            KillEventManager.Subscribe();
        }

        private void OnDestroy()
        {
            // 取消订阅击杀事件
            KillEventManager.Unsubscribe();
            
            harmony.UnpatchSelf();
            if (hitTexture != null)
            {
                try { Destroy(hitTexture); } catch { }
                hitTexture = null;
            }
            
            if (skullTexture != null)
            {
                try { Destroy(skullTexture); } catch { }
                skullTexture = null;
            }
            
            if (redSkullTexture != null && redSkullTexture != skullTexture)
            {
                try { Destroy(redSkullTexture); } catch { }
                redSkullTexture = null;
            }
            
            KillFeedUI?.Cleanup();
            KillFeedUI = null;
        }

        public void Update()
        {
            HandleDebugInput();
            // 更新击杀提示系统
            KillFeedUI?.Update();
        }

        void OnGUI()
        {
            if (hitDetected && Time.time - hitTime < HitDuration.Value)
            {
                float hitDurationValue = HitDuration.Value;
                float t = (Time.time - hitTime) / hitDurationValue;
                float alpha = 1f - t;
                float animationScale = HitMarkerAnimationScale.Value;
                float scale = Mathf.Lerp(animationScale, 1f, t); // pop animation
                float size = HitBaseSize.Value * scale;

                Color originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                Vector2 center = new Vector2(
                    Screen.width * 0.5f + HitMarkerCenterOffset.Value.x,
                    Screen.height * 0.5f + HitMarkerCenterOffset.Value.y
                );

                Rect drawRect = new Rect(center.x - size / 2f, center.y - size / 2f, size, size);

                if (hitTexture != null)
                {
                    GUI.DrawTexture(drawRect, hitTexture, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    // fallback: simple white X at center
                    float w = size;
                    float h = 3f;
                    // draw horizontal line
                    GUI.DrawTexture(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), Texture2D.whiteTexture);
                    // draw vertical line by rotating GUI around pivot
                    GUIUtility.RotateAroundPivot(45f, center);
                    GUI.DrawTexture(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), Texture2D.whiteTexture);
                    GUIUtility.RotateAroundPivot(-45f, center);
                }

                if (ShowDamageText.Value)
                {
                    var entries = GetDamageEntriesSnapshot(Time.time);
                    if (entries != null && entries.Count > 0)
                    {
                        try
                        {
                            DrawDamageNumbers(drawRect, entries);
                        }
                        catch (Exception ex)
                        {
                            try { Log?.LogDebug($"[SimpleHitMarker] Failed to draw damage numbers: {ex}"); } catch { }
                        }
                    }
                }

                GUI.color = originalColor;
            }
            else if (hitDetected)
            {
                hitDetected = false;
                ClearDamageEntries();
            }
            
            // 绘制击杀提示
            KillFeedUI?.OnGUI();
        }

        private static List<DamageDisplayEntry> GetDamageEntriesSnapshot(float now)
        {
            lock (DamageEntriesLock)
            {
                PruneDamageEntriesLocked(now);
                if (DamageEntries.Count == 0)
                {
                    return null;
                }

                return new List<DamageDisplayEntry>(DamageEntries);
            }
        }

        internal static void RegisterDamageEvent(float damageAmount, Vector3 hitPoint, bool isHeadshot)
        {
            hitDetected = true;
            hitTime = Time.time;
            hitWorldPoint = hitPoint;
            hitDamage = damageAmount;
            AddDamageEntry(damageAmount, isHeadshot);
            
            // 播放命中音效
            PlayHitSound(isHeadshot);
        }
        
        /// <summary>
        /// 播放命中音效
        /// </summary>
        private static void PlayHitSound(bool isHeadshot)
        {
            // 检查配置：如果未启用或配置为 false，则不播放
            if (EnableHitSound?.Value == false)
            {
                Log?.LogDebug("[SimpleHitMarker] Hit sound disabled by config");
                return;
            }

            // 获取或创建 AudioSource
            AudioSource source = GetOrCreateAudioSource();
            if (source == null)
            {
                Log?.LogWarning("[SimpleHitMarker] Failed to get or create AudioSource, cannot play hit sound");
                return;
            }

            try
            {
                // 检查并重新加载音频剪辑（如果需要）
                if (!IsAudioClipValid(hitSoundClip) || !IsAudioClipValid(headshotHitSoundClip))
                {
                    Log?.LogWarning("[SimpleHitMarker] Audio clips invalid, reloading...");
                    ReloadAudioClipsStatic();
                }
                
                AudioClip clipToPlay = null;
                
                if (isHeadshot && IsAudioClipValid(headshotHitSoundClip))
                {
                    clipToPlay = headshotHitSoundClip;
                    Log?.LogDebug("[SimpleHitMarker] Playing headshot hit sound");
                }
                else if (IsAudioClipValid(hitSoundClip))
                {
                    clipToPlay = hitSoundClip;
                    Log?.LogDebug("[SimpleHitMarker] Playing regular hit sound");
                }
                else
                {
                    Log?.LogWarning("[SimpleHitMarker] No valid hit sound clip available to play");
                    return;
                }

                if (clipToPlay != null && IsAudioClipValid(clipToPlay))
                {
                    float volume = HitSoundVolume?.Value ?? 0.5f;
                    Log?.LogInfo($"[SimpleHitMarker] Playing hit sound: clip={clipToPlay.name}, length={clipToPlay.length}, volume={volume}, isHeadshot={isHeadshot}");
                    source.PlayOneShot(clipToPlay, volume);
                }
                else
                {
                    Log?.LogWarning("[SimpleHitMarker] Selected clip is invalid, cannot play");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[SimpleHitMarker] Error playing hit sound: {ex}");
            }
        }
        
        /// <summary>
        /// 播放击杀音效（供 KillPatch 调用）
        /// </summary>
        public static void PlayKillSound(bool isHeadshot)
        {
            // 检查配置：如果未启用或配置为 false，则不播放
            if (EnableKillSound?.Value == false)
            {
                Log?.LogDebug("[SimpleHitMarker] Kill sound disabled by config");
                return;
            }

            // 获取或创建 AudioSource
            AudioSource source = GetOrCreateAudioSource();
            if (source == null)
            {
                Log?.LogWarning("[SimpleHitMarker] Failed to get or create AudioSource, cannot play kill sound");
                return;
            }

            try
            {
                // 检查并重新加载音频剪辑（如果需要）
                if (!IsAudioClipValid(killSoundClip) || !IsAudioClipValid(headshotKillSoundClip) || 
                    !IsAudioClipValid(hitSoundClip) || !IsAudioClipValid(headshotHitSoundClip))
                {
                    Log?.LogWarning("[SimpleHitMarker] Audio clips invalid, reloading...");
                    ReloadAudioClipsStatic();
                }
                
                AudioClip clipToPlay = null;
                
                if (isHeadshot && IsAudioClipValid(headshotKillSoundClip))
                {
                    clipToPlay = headshotKillSoundClip;
                    Log?.LogDebug("[SimpleHitMarker] Playing headshot kill sound");
                }
                else if (IsAudioClipValid(killSoundClip))
                {
                    clipToPlay = killSoundClip;
                    Log?.LogDebug("[SimpleHitMarker] Playing regular kill sound");
                }
                else if (isHeadshot && IsAudioClipValid(headshotHitSoundClip))
                {
                    // 如果没有爆头击杀音效，使用爆头命中音效作为回退
                    clipToPlay = headshotHitSoundClip;
                    Log?.LogDebug("[SimpleHitMarker] Using headshot hit sound as fallback for kill");
                }
                else if (IsAudioClipValid(hitSoundClip))
                {
                    // 如果没有击杀音效，使用命中音效作为回退
                    clipToPlay = hitSoundClip;
                    Log?.LogDebug("[SimpleHitMarker] Using hit sound as fallback for kill");
                }
                else
                {
                    Log?.LogWarning("[SimpleHitMarker] No valid kill sound clip available to play");
                    return;
                }

                if (clipToPlay != null && IsAudioClipValid(clipToPlay))
                {
                    float volume = KillSoundVolume?.Value ?? 0.7f;
                    Log?.LogInfo($"[SimpleHitMarker] Playing kill sound: clip={clipToPlay.name}, length={clipToPlay.length}, volume={volume}, isHeadshot={isHeadshot}");
                    source.PlayOneShot(clipToPlay, volume);
                }
                else
                {
                    Log?.LogWarning("[SimpleHitMarker] Selected clip is invalid, cannot play");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"[SimpleHitMarker] Error playing kill sound: {ex}");
            }
        }

        private static void AddDamageEntry(float damageAmount, bool isHeadshot)
        {
            float now = Time.time;
            lock (DamageEntriesLock)
            {
                PruneDamageEntriesLocked(now);
                DamageEntries.Add(new DamageDisplayEntry
                {
                    Damage = damageAmount,
                    IsHeadshot = isHeadshot,
                    Timestamp = now
                });
            }
        }

        private static void PruneDamageEntriesLocked(float currentTime)
        {
            if (DamageEntries.Count == 0)
            {
                return;
            }

            float lifetime = HitDuration?.Value ?? 0.5f;
            for (int i = DamageEntries.Count - 1; i >= 0; i--)
            {
                if (currentTime - DamageEntries[i].Timestamp > lifetime)
                {
                    DamageEntries.RemoveAt(i);
                }
            }
        }

        private static void ClearDamageEntries()
        {
            lock (DamageEntriesLock)
            {
                DamageEntries.Clear();
            }
        }

        private void DrawDamageNumbers(Rect iconRect, List<DamageDisplayEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            Color damageColor = DamageTextColor.Value;
            int fontSize = Mathf.Max(1, DamageTextSize.Value);

            GUIStyle fillStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = fontSize
            };
            fillStyle.normal.textColor = new Color(damageColor.r, damageColor.g, damageColor.b, 1f);

            GUIStyle outlineStyle = new GUIStyle(fillStyle);

            float paddingFromMarker = DamageTextPadding.Value;
            float spacingBetweenNumbers = Mathf.Clamp(DamageMultiTextPadding.Value, 0f, 200f);
            float outlineAlpha = Mathf.Clamp01(DamageTextOutlineOpacity.Value);
            float outlineThickness = Mathf.Clamp(DamageTextOutlineThickness.Value, 0.5f, 10f);
            float currentX = iconRect.xMax + paddingFromMarker;
            float centerY = iconRect.center.y;

            GUIContent content = new GUIContent();
            Color originalGuiColor = GUI.color;
            GUI.color = Color.white;

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
                    ? DamageTextHeadshotOutlineColor.Value
                    : DamageTextOutlineColor.Value;
                baseOutlineColor.a *= outlineAlpha;
                outlineStyle.normal.textColor = baseOutlineColor;

                DrawOutlinedLabel(textRect, content, fillStyle, outlineStyle, outlineThickness);
                currentX += textSize.x + spacingBetweenNumbers;
            }

            GUI.color = originalGuiColor;
        }

        /// <summary>
        /// 绘制带描边的标签
        /// </summary>
        /// <param name="rect">标签矩形区域</param>
        /// <param name="content">标签内容</param>
        /// <param name="fillStyle">填充样式</param>
        /// <param name="outlineStyle">描边样式</param>
        /// <param name="outlineThickness">描边厚度</param>
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

        private void HandleDebugInput()
        {
            if (DebugTriggerKey == null)
            {
                return;
            }

            var shortcut = DebugTriggerKey.Value;
            if (!shortcut.IsDown())
            {
                return;
            }

            GenerateDebugHit();
            GenerateDebugKill();
        }

        private void GenerateDebugHit()
        {
            bool isHeadshot = DebugRandom.NextDouble() > 0.7;
            float damage = Mathf.Round(RandomRange(25f, 120f));
            RegisterDamageEvent(damage, Vector3.zero, isHeadshot);
        }

        private void GenerateDebugKill()
        {
            if (KillFeedUI == null)
            {
                return;
            }

            bool isHeadshot = DebugRandom.NextDouble() > 0.55;
            EBodyPart bodyPart = isHeadshot ? EBodyPart.Head : PickRandom(DebugBodyParts, part => part != EBodyPart.Head);

            var killInfo = new KillInfo
            {
                BodyPart = bodyPart,
                IsHeadshot = isHeadshot,
                Distance = Mathf.Round(RandomRange(5f, 250f)),
                Experience = Mathf.RoundToInt(RandomRange(80f, 220f)),
                KillTime = Time.time,
                KillMethod = PickRandom(DebugKillMethods),
                PlayerName = PickRandom(DebugNames),
                PlayerLevel = Mathf.RoundToInt(RandomRange(1f, 65f)),
                Faction = PickRandom(DebugFactions)
            };

            killInfo.FactionIcon = GetPmcRankIcon(killInfo.Faction, killInfo.PlayerLevel);

            KillFeedUI.AddKill(killInfo);
            Log?.LogInfo("[SimpleHitMarker] Debug kill generated via hotkey.");
        }

        private static float RandomRange(float min, float max)
        {
            return (float)(DebugRandom.NextDouble() * (max - min) + min);
        }

        private static T PickRandom<T>(T[] source)
        {
            if (source == null || source.Length == 0)
            {
                return default;
            }

            return source[DebugRandom.Next(source.Length)];
        }

        private static T PickRandom<T>(T[] source, Func<T, bool> predicate)
        {
            if (source == null || source.Length == 0)
            {
                return default;
            }

            var filtered = Array.FindAll(source, item => predicate == null || predicate(item));
            if (filtered.Length == 0)
            {
                return source[DebugRandom.Next(source.Length)];
            }

            return filtered[DebugRandom.Next(filtered.Length)];
        }
    }
}
