using BepInEx;
using BepInEx.Configuration;
using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using System.IO;
using UnityEngine;
using static HairRenderer;
using SimpleHitMarker.KillFeed;
using SimpleHitmarker.KillPatch;

namespace SimpleHitMarker
{
    [BepInPlugin("com.shiunaya.simplehitmarker", "shm", "0.0.1")]
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
        public static ConfigEntry<Color> DamageTextColor { get; set; }
        
        // Kill Feed 位置配置
        public static ConfigEntry<float> KillFeedStartX { get; set; }
        public static ConfigEntry<float> KillFeedStartY { get; set; }
        public static ConfigEntry<float> KillFeedLineHeight { get; set; }
        public static ConfigEntry<float> KillFeedLineSpacing { get; set; }
        
        // Kill Feed 时间配置
        public static ConfigEntry<float> KillFeedDuration { get; set; }
        public static ConfigEntry<float> SkullDisplayDuration { get; set; }
        public static ConfigEntry<float> SkullFadeDuration { get; set; }
        public static ConfigEntry<float> StreakWindow { get; set; }
        
        // Kill Feed 骷髅头配置
        public static ConfigEntry<float> SkullSize { get; set; }
        public static ConfigEntry<float> SkullSpacing { get; set; }
        public static ConfigEntry<float> SkullAnimationSpeed { get; set; }
        
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
        
        // Kill Feed 其他配置
        public static ConfigEntry<float> FactionIconSize { get; set; }
        public static ConfigEntry<float> ExperienceTextWidth { get; set; }

        // Debug 配置
        public static ConfigEntry<KeyboardShortcut> DebugTriggerKey { get; set; }

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
                Log.LogInfo($"[shm] Looking for hit texture at: {hitPngpath}");
                hitTexture = TextureLoader.LoadTextureFromFile(hitPngpath);
                if (hitTexture == null)
                {
                    //也尝试 DLL 同目录
                    string alt = Path.Combine(assemblyDir, "hit.png");
                    Log.LogInfo($"[shm] Trying alternate path: {alt}");
                    hitTexture = TextureLoader.LoadTextureFromFile(alt);
                }

                if (hitTexture == null)
                {
                    Log.LogWarning("[shm] hit.png not found in plugin folder or SimpleHitMarker subfolder. Using simple X fallback.");
                }
            }
            catch (Exception ex)
            {
                try { Log.LogError($"[shm] Texture load error: {ex}"); } catch { }
            }
            
            // 加载骷髅头纹理
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                
                // 加载普通骷髅头
                string skullPath = Path.Combine(assemblyDir, "skull.png");
                skullTexture = TextureLoader.LoadTextureFromFile(skullPath);
                if (skullTexture == null)
                {
                    Log.LogWarning("[shm] skull.png not found. Kill feed skull will not display.");
                }
                
                // 加载红色骷髅头（爆头）
                string redSkullPath = Path.Combine(assemblyDir, "skull_head.png");
                redSkullTexture = TextureLoader.LoadTextureFromFile(redSkullPath);
                if (redSkullTexture == null)
                {
                    // 如果没有红色骷髅头，使用普通骷髅头
                    redSkullTexture = skullTexture;
                    Log.LogInfo("[shm] skull_head.png not found. Using regular skull for headshots.");
                }
            }
            catch (Exception ex)
            {
                try { Log.LogError($"[shm] Skull texture load error: {ex}"); } catch { }
            }
            
            harmony = new Harmony("com.shiunaya.simplehitmarker");
            harmony.PatchAll();
            
            Log.LogInfo("SimpleHitMarker Plugin is loaded!");
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
                0.5f,
                new ConfigDescription(
                    "击中标记显示的时间长度（秒）",
                    new AcceptableValueRange<float>(0.1f, 5.0f),
                    new ConfigurationManagerAttributes { Order = 100 }
                )
            );

            HitBaseSize = Config.Bind<float>(
                "Hit Marker",
                "基础大小",
                128f,
                new ConfigDescription(
                    "击中标记的基础像素大小",
                    new AcceptableValueRange<float>(32f, 512f),
                    new ConfigurationManagerAttributes { Order = 90 }
                )
            );

            HitMarkerCenterOffset = Config.Bind<Vector2>(
                "Hit Marker",
                "中心偏移",
                Vector2.zero,
                new ConfigDescription(
                    "击中标记相对屏幕中心的偏移（像素）",
                    null,
                    new ConfigurationManagerAttributes { Order = 80, IsAdvanced = true }
                )
            );

            HitMarkerAnimationScale = Config.Bind<float>(
                "Hit Marker",
                "动画缩放",
                1.4f,
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
                12,
                new ConfigDescription(
                    "伤害文字的最小字体大小",
                    new AcceptableValueRange<int>(8, 24),
                    new ConfigurationManagerAttributes { Order = 40, IsAdvanced = true }
                )
            );

            DamageTextMaxSize = Config.Bind<int>(
                "Hit Marker",
                "伤害文字最大字号",
                48,
                new ConfigDescription(
                    "伤害文字的最大字体大小",
                    new AcceptableValueRange<int>(24, 96),
                    new ConfigurationManagerAttributes { Order = 30, IsAdvanced = true }
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

            // ============ Kill Feed 位置配置 ============
            KillFeedStartX = Config.Bind<float>(
                "Kill Feed",
                "起始X位置",
                50f,
                new ConfigDescription(
                    "击杀提示列表的起始X坐标（像素）",
                    new AcceptableValueRange<float>(0f, 2000f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "位置" }
                )
            );

            KillFeedStartY = Config.Bind<float>(
                "Kill Feed",
                "起始Y位置",
                100f,
                new ConfigDescription(
                    "击杀提示列表的起始Y坐标（像素）",
                    new AcceptableValueRange<float>(0f, 2000f),
                    new ConfigurationManagerAttributes { Order = 90, Category = "位置" }
                )
            );

            KillFeedLineHeight = Config.Bind<float>(
                "Kill Feed",
                "行高",
                30f,
                new ConfigDescription(
                    "每行击杀信息的高度（像素）",
                    new AcceptableValueRange<float>(10f, 100f),
                    new ConfigurationManagerAttributes { Order = 80, Category = "位置" }
                )
            );

            KillFeedLineSpacing = Config.Bind<float>(
                "Kill Feed",
                "行间距",
                8f,
                new ConfigDescription(
                    "每行之间的间距（像素）",
                    new AcceptableValueRange<float>(0f, 50f),
                    new ConfigurationManagerAttributes { Order = 70, Category = "位置" }
                )
            );

            // ============ Kill Feed 时间配置 ============
            KillFeedDuration = Config.Bind<float>(
                "Kill Feed",
                "显示时长",
                5f,
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
                64f,
                new ConfigDescription(
                    "骷髅头图标的像素大小",
                    new AcceptableValueRange<float>(16f, 256f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "骷髅头" }
                )
            );

            SkullSpacing = Config.Bind<float>(
                "Kill Feed",
                "骷髅头间距",
                60f,
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

            // ============ Kill Feed 字体配置 ============
            FontSizeFaction = Config.Bind<int>(
                "Kill Feed",
                "阵营文字字号",
                16,
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
                14,
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
                new Color(1f, 1f, 0.8f, 1f),
                new ConfigDescription(
                    "经验值文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 90, Category = "颜色" }
                )
            );

            ColorPlayerName = Config.Bind<Color>(
                "Kill Feed",
                "玩家名称颜色",
                new Color(1f, 0.3f, 0.3f, 1f),
                new ConfigDescription(
                    "玩家名称文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 80, Category = "颜色" }
                )
            );

            ColorKillDetails = Config.Bind<Color>(
                "Kill Feed",
                "击杀详情颜色",
                new Color(0.8f, 0.8f, 0.8f, 1f),
                new ConfigDescription(
                    "击杀详情文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 70, Category = "颜色" }
                )
            );

            // ============ Kill Feed 其他配置 ============
            FactionIconSize = Config.Bind<float>(
                "Kill Feed",
                "阵营图标大小",
                24f,
                new ConfigDescription(
                    "阵营图标的大小（像素）",
                    new AcceptableValueRange<float>(8f, 128f),
                    new ConfigurationManagerAttributes { Order = 60, IsAdvanced = true }
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

                // Draw damage text to right of icon
                if (ShowDamageText.Value)
                {
                    try
                    {
                        string dmgText = hitDamage.ToString("0");
                        GUIStyle style = new GUIStyle(GUI.skin.label);
                        style.alignment = TextAnchor.MiddleLeft;
                        
                        Color damageColor = DamageTextColor.Value;
                        style.normal.textColor = new Color(damageColor.r, damageColor.g, damageColor.b, alpha);
                        
                        int fontSize = Mathf.Clamp(
                            Mathf.RoundToInt(size * 0.25f),
                            DamageTextMinSize.Value,
                            DamageTextMaxSize.Value
                        );
                        style.fontSize = fontSize;

                        float padding = DamageTextPadding.Value;
                        Rect textRect = new Rect(
                            drawRect.xMax + padding,
                            drawRect.center.y - (fontSize / 2f),
                            200f,
                            fontSize + 4f
                        );
                        GUI.Label(textRect, dmgText, style);
                    }
                    catch { }
                }

                GUI.color = originalColor;
            }
            else if (hitDetected)
            {
                hitDetected = false;
            }
            
            // 绘制击杀提示
            KillFeedUI?.OnGUI();
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
            hitDetected = true;
            hitTime = Time.time;
            hitWorldPoint = Vector3.zero;
            hitDamage = Mathf.Round(RandomRange(25f, 120f));
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

            KillFeedUI.AddKill(killInfo);
            Log?.LogInfo("[shm] Debug kill generated via hotkey.");
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
