using BepInEx.Configuration;
using UnityEngine;
using System.IO;

namespace SimpleHitMarker
{
    public class ConfigurationManager
    {
        public ConfigEntry<float> HitDuration { get; private set; }
        public ConfigEntry<float> HitBaseSize { get; private set; }
        public ConfigEntry<Vector2> HitMarkerCenterOffset { get; private set; }
        public ConfigEntry<float> HitMarkerAnimationScale { get; private set; }
        public ConfigEntry<bool> ShowDamageText { get; private set; }
        public ConfigEntry<float> DamageTextPadding { get; private set; }
        public ConfigEntry<int> DamageTextMinSize { get; private set; }
        public ConfigEntry<int> DamageTextMaxSize { get; private set; }
        public ConfigEntry<int> DamageTextSize { get; private set; }
        public ConfigEntry<Color> DamageTextColor { get; private set; }
        public ConfigEntry<Color> DamageTextOutlineColor { get; private set; }
        public ConfigEntry<Color> DamageTextHeadshotOutlineColor { get; private set; }
        public ConfigEntry<float> DamageMultiTextPadding { get; private set; }
        public ConfigEntry<float> DamageTextOutlineOpacity { get; private set; }
        public ConfigEntry<float> DamageTextOutlineThickness { get; private set; }

        public ConfigEntry<float> KillFeedHorizontalOffset { get; private set; }
        public ConfigEntry<float> KillFeedVerticalOffset { get; private set; }
        public ConfigEntry<float> KillFeedLineSpacing { get; private set; }
        public ConfigEntry<float> KillFeedBlockWidth { get; private set; }
        public ConfigEntry<float> KillFeedExperienceHorizontalOffset { get; private set; }

        public ConfigEntry<float> KillFeedDuration { get; private set; }
        public ConfigEntry<float> SkullDisplayDuration { get; private set; }
        public ConfigEntry<float> SkullFadeDuration { get; private set; }
        public ConfigEntry<float> StreakWindow { get; private set; }

        public ConfigEntry<float> SkullSize { get; private set; }
        public ConfigEntry<float> SkullSpacing { get; private set; }
        public ConfigEntry<float> SkullAnimationSpeed { get; private set; }
        public ConfigEntry<float> SkullPushAnimationSpeed { get; private set; }

        public ConfigEntry<int> FontSizeFaction { get; private set; }
        public ConfigEntry<int> FontSizeExperience { get; private set; }
        public ConfigEntry<int> FontSizePlayerName { get; private set; }
        public ConfigEntry<int> FontSizeKillDetails { get; private set; }

        public ConfigEntry<Color> ColorFaction { get; private set; }
        public ConfigEntry<Color> ColorExperience { get; private set; }
        public ConfigEntry<Color> ColorPlayerName { get; private set; }
        public ConfigEntry<Color> ColorKillDetails { get; private set; }

        public ConfigEntry<Color> ColorUSEC { get; private set; }
        public ConfigEntry<Color> ColorBEAR { get; private set; }
        public ConfigEntry<Color> ColorSCAV { get; private set; }
        public ConfigEntry<Color> ColorBOSS { get; private set; }
        public ConfigEntry<Color> ColorFollower { get; private set; }
        public ConfigEntry<Color> ColorRAIDER { get; private set; }
        public ConfigEntry<Color> ColorRouges { get; private set; }
        public ConfigEntry<Color> ColorSectant { get; private set; }

        public ConfigEntry<float> FactionIconSize { get; private set; }
        public ConfigEntry<float> FactionIconVerticalOffset { get; private set; }
        public ConfigEntry<float> ExperienceTextWidth { get; private set; }

        public ConfigEntry<float> NameOutlineOpacity { get; private set; }
        public ConfigEntry<float> NameOutlineThickness { get; private set; }

        public ConfigEntry<KeyboardShortcut> DebugTriggerKey { get; private set; }
        public ConfigEntry<bool> DebugMode { get; private set; }

        public ConfigEntry<bool> EnableHitSound { get; private set; }
        public ConfigEntry<bool> EnableKillSound { get; private set; }
        public ConfigEntry<float> HitSoundVolume { get; private set; }
        public ConfigEntry<float> KillSoundVolume { get; private set; }

        private readonly ConfigFile _config;

        public ConfigurationManager(ConfigFile config)
        {
            _config = config;
            InitializeConfigs();
        }

        private void InitializeConfigs()
        {
            // ============ Hit Marker 配置 ============
            HitDuration = _config.Bind<float>(
                "Hit Marker",
                "显示时长",
                0.8680753f,
                new ConfigDescription(
                    "击中标记显示的时间长度（秒）",
                    new AcceptableValueRange<float>(0.1f, 5.0f),
                    new ConfigurationManagerAttributes { Order = 100 }
                )
            );

            HitBaseSize = _config.Bind<float>(
                "Hit Marker",
                "基础大小",
                71f,
                new ConfigDescription(
                    "击中标记的基础像素大小",
                    new AcceptableValueRange<float>(32f, 512f),
                    new ConfigurationManagerAttributes { Order = 90 }
                )
            );

            HitMarkerCenterOffset = _config.Bind<Vector2>(
                "Hit Marker",
                "中心偏移",
                new Vector2(0f, 5f),
                new ConfigDescription(
                    "击中标记相对屏幕中心的偏移（像素）",
                    null,
                    new ConfigurationManagerAttributes { Order = 80, IsAdvanced = true }
                )
            );

            HitMarkerAnimationScale = _config.Bind<float>(
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
            ShowDamageText = _config.Bind<bool>(
                "Hit Marker",
                "显示伤害文字",
                true,
                new ConfigDescription(
                    "是否在击中标记旁显示伤害数值",
                    null,
                    new ConfigurationManagerAttributes { Order = 60 }
                )
            );

            DamageTextPadding = _config.Bind<float>(
                "Hit Marker",
                "伤害文字间距",
                8f,
                new ConfigDescription(
                    "伤害文字与击中标记之间的间距（像素）",
                    new AcceptableValueRange<float>(0f, 50f),
                    new ConfigurationManagerAttributes { Order = 50, IsAdvanced = true }
                )
            );

            DamageTextMinSize = _config.Bind<int>(
                "Hit Marker",
                "伤害文字最小字号",
                15,
                new ConfigDescription(
                    "伤害文字的最小字体大小",
                    new AcceptableValueRange<int>(8, 24),
                    new ConfigurationManagerAttributes { Order = 40, IsAdvanced = true }
                )
            );

            DamageTextMaxSize = _config.Bind<int>(
                "Hit Marker",
                "伤害文字最大字号",
                73,
                new ConfigDescription(
                    "伤害文字的最大字体大小",
                    new AcceptableValueRange<int>(24, 96),
                    new ConfigurationManagerAttributes { Order = 30, IsAdvanced = true }
                )
            );

            DamageTextSize = _config.Bind<int>(
                "Hit Marker",
                "伤害文字字号",
                21,
                new ConfigDescription(
                    "伤害文字的固定字体大小",
                    new AcceptableValueRange<int>(8, 96),
                    new ConfigurationManagerAttributes { Order = 25 }
                )
            );

            DamageTextColor = _config.Bind<Color>(
                "Hit Marker",
                "伤害文字颜色",
                Color.white,
                new ConfigDescription(
                    "伤害文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 20, IsAdvanced = true }
                )
            );

            DamageTextOutlineColor = _config.Bind<Color>(
                "Hit Marker",
                "伤害文字描边颜色",
                Color.white,
                new ConfigDescription(
                    "伤害文字描边的基础颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 19, IsAdvanced = true }
                )
            );

            DamageTextHeadshotOutlineColor = _config.Bind<Color>(
                "Hit Marker",
                "爆头伤害描边颜色",
                Color.red,
                new ConfigDescription(
                    "爆头伤害文字描边颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 18, IsAdvanced = true }
                )
            );

            DamageMultiTextPadding = _config.Bind<float>(
                "Hit Marker",
                "多段伤害间距",
                12f,
                new ConfigDescription(
                    "同一显示窗口内多段伤害数字之间的间距（像素）",
                    new AcceptableValueRange<float>(0f, 100f),
                    new ConfigurationManagerAttributes { Order = 17, IsAdvanced = true }
                )
            );

            DamageTextOutlineOpacity = _config.Bind<float>(
                "Hit Marker",
                "描边不透明度",
                0.2535212f,
                new ConfigDescription(
                    "伤害文字描边颜色的不透明度（0-1）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 16, IsAdvanced = true }
                )
            );

            DamageTextOutlineThickness = _config.Bind<float>(
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
            KillFeedHorizontalOffset = _config.Bind<float>(
                "Kill Feed",
                "水平中心偏移",
                107f,
                new ConfigDescription(
                    "击杀提示相对屏幕中心向左的偏移量（像素）。正值向左移动，负值向右移动。",
                    new AcceptableValueRange<float>(-2000f, 2000f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "位置" }
                )
            );

            KillFeedVerticalOffset = _config.Bind<float>(
                "Kill Feed",
                "垂直中心偏移",
                0f,
                new ConfigDescription(
                    "击杀提示相对屏幕垂直中心的偏移量（像素）。正值向下移动，负值向上移动。",
                    new AcceptableValueRange<float>(-2000f, 2000f),
                    new ConfigurationManagerAttributes { Order = 90, Category = "位置" }
                )
            );


            KillFeedLineSpacing = _config.Bind<float>(
                "Kill Feed",
                "行间距",
                0f,
                new ConfigDescription(
                    "每行之间的间距（像素）",
                    new AcceptableValueRange<float>(0f, 50f),
                    new ConfigurationManagerAttributes { Order = 70, Category = "位置" }
                )
            );

            KillFeedBlockWidth = _config.Bind<float>(
                "Kill Feed",
                "内容宽度",
                420f,
                new ConfigDescription(
                    "每行文字内容的最大宽度（像素），用于右对齐布局。",
                    new AcceptableValueRange<float>(100f, 1000f),
                    new ConfigurationManagerAttributes { Order = 65, Category = "位置" }
                )
            );

            KillFeedExperienceHorizontalOffset = _config.Bind<float>(
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
            KillFeedDuration = _config.Bind<float>(
                "Kill Feed",
                "显示时长",
                6f,
                new ConfigDescription(
                    "每个击杀提示显示的时间长度（秒）",
                    new AcceptableValueRange<float>(1f, 30f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "时间" }
                )
            );

            SkullDisplayDuration = _config.Bind<float>(
                "Kill Feed",
                "骷髅头显示时长",
                2f,
                new ConfigDescription(
                    "骷髅头图标显示的时间长度（秒）",
                    new AcceptableValueRange<float>(0.5f, 10f),
                    new ConfigurationManagerAttributes { Order = 90, Category = "时间" }
                )
            );

            SkullFadeDuration = _config.Bind<float>(
                "Kill Feed",
                "骷髅头淡出时长",
                0.3f,
                new ConfigDescription(
                    "骷髅头淡出动画的时间长度（秒）",
                    new AcceptableValueRange<float>(0.1f, 2f),
                    new ConfigurationManagerAttributes { Order = 80, Category = "时间" }
                )
            );

            StreakWindow = _config.Bind<float>(
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
            SkullSize = _config.Bind<float>(
                "Kill Feed",
                "骷髅头大小",
                40f,
                new ConfigDescription(
                    "骷髅头图标的像素大小",
                    new AcceptableValueRange<float>(16f, 256f),
                    new ConfigurationManagerAttributes { Order = 100, Category = "骷髅头" }
                )
            );

            SkullSpacing = _config.Bind<float>(
                "Kill Feed",
                "骷髅头间距",
                20f,
                new ConfigDescription(
                    "连杀时骷髅头之间的间距（像素）",
                    new AcceptableValueRange<float>(20f, 200f),
                    new ConfigurationManagerAttributes { Order = 90, Category = "骷髅头" }
                )
            );

            SkullAnimationSpeed = _config.Bind<float>(
                "Kill Feed",
                "骷髅头动画速度",
                5f,
                new ConfigDescription(
                    "骷髅头位置动画的速度",
                    new AcceptableValueRange<float>(1f, 20f),
                    new ConfigurationManagerAttributes { Order = 80, Category = "骷髅头", IsAdvanced = true }
                )
            );

            SkullPushAnimationSpeed = _config.Bind<float>(
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
            FontSizeFaction = _config.Bind<int>(
                "Kill Feed",
                "阵营文字字号",
                20,
                new ConfigDescription(
                    "阵营和等级文字的字体大小",
                    new AcceptableValueRange<int>(8, 48),
                    new ConfigurationManagerAttributes { Order = 100, Category = "字体" }
                )
            );

            FontSizeExperience = _config.Bind<int>(
                "Kill Feed",
                "经验值字号",
                20,
                new ConfigDescription(
                    "经验值文字的字体大小",
                    new AcceptableValueRange<int>(8, 48),
                    new ConfigurationManagerAttributes { Order = 90, Category = "字体" }
                )
            );

            FontSizePlayerName = _config.Bind<int>(
                "Kill Feed",
                "玩家名字号",
                18,
                new ConfigDescription(
                    "玩家名称文字的字体大小",
                    new AcceptableValueRange<int>(8, 48),
                    new ConfigurationManagerAttributes { Order = 80, Category = "字体" }
                )
            );

            FontSizeKillDetails = _config.Bind<int>(
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
            ColorFaction = _config.Bind<Color>(
                "Kill Feed",
                "阵营文字颜色",
                Color.white,
                new ConfigDescription(
                    "阵营和等级文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 100, Category = "颜色" }
                )
            );

            ColorExperience = _config.Bind<Color>(
                "Kill Feed",
                "经验值颜色",
                new Color32(0xFF, 0xCC, 0xFF, 0xFF),
                new ConfigDescription(
                    "经验值文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 90, Category = "颜色" }
                )
            );

            ColorPlayerName = _config.Bind<Color>(
                "Kill Feed",
                "玩家名称颜色",
                new Color32(0xFF, 0x00, 0x0F, 0xFF),
                new ConfigDescription(
                    "玩家名称文字的显示颜色",
                    null,
                    new ConfigurationManagerAttributes { Order = 80, Category = "颜色" }
                )
            );

            ColorKillDetails = _config.Bind<Color>(
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
            ColorUSEC = _config.Bind<Color>(
                "Kill Feed",
                "颜色_USEC",
                new Color(0.0f, 0.2f, 0.6f, 1f), // deep blue
                new ConfigDescription("USEC 文字颜色", null, new ConfigurationManagerAttributes { Order = 69, Category = "颜色" })
            );

            ColorBEAR = _config.Bind<Color>(
                "Kill Feed",
                "颜色_BEAR",
                new Color(1f, 0.55f, 0f, 1f), // orange
                new ConfigDescription("BEAR 文字颜色", null, new ConfigurationManagerAttributes { Order = 68, Category = "颜色" })
            );

            ColorSCAV = _config.Bind<Color>(
                "Kill Feed",
                "颜色_SCAV",
                new Color(1f, 0.9f, 0f, 1f), // yellow
                new ConfigDescription("SCAV 文字颜色", null, new ConfigurationManagerAttributes { Order = 67, Category = "颜色" })
            );

            ColorBOSS = _config.Bind<Color>(
                "Kill Feed",
                "颜色_BOSS",
                new Color(1f, 0f, 0f, 1f), // red
                new ConfigDescription("BOSS 文字颜色", null, new ConfigurationManagerAttributes { Order = 66, Category = "颜色" })
            );

            ColorFollower = _config.Bind<Color>(
                "Kill Feed",
                "颜色_Follower",
                new Color(0.9f, 0.2f, 0.2f, 1f), // slightly lighter red
                new ConfigDescription("Follower 文字颜色", null, new ConfigurationManagerAttributes { Order = 65, Category = "颜色" })
            );

            ColorRAIDER = _config.Bind<Color>(
                "Kill Feed",
                "颜色_RAIDER",
                new Color(1f, 0.4f, 0.7f, 1f), // pink
                new ConfigDescription("RAIDER 文字颜色", null, new ConfigurationManagerAttributes { Order = 64, Category = "颜色" })
            );

            ColorRouges = _config.Bind<Color>(
                "Kill Feed",
                "颜色_Rouges",
                new Color(0.2f, 0.2f, 0.2f, 1f), // dark gray
                new ConfigDescription("Rouges 文字颜色", null, new ConfigurationManagerAttributes { Order = 63, Category = "颜色" })
            );

            ColorSectant = _config.Bind<Color>(
                "Kill Feed",
                "颜色_Sectant",
                new Color(0f, 0.4f, 0f, 1f), // dark green
                new ConfigDescription("Sectant 文字颜色", null, new ConfigurationManagerAttributes { Order = 62, Category = "颜色" })
            );

            // ============ Kill Feed 其他配置 ============
            FactionIconSize = _config.Bind<float>(
                "Kill Feed",
                "阵营图标大小",
                86f,
                new ConfigDescription(
                    "阵营图标的大小（像素）",
                    new AcceptableValueRange<float>(8f, 128f),
                    new ConfigurationManagerAttributes { Order = 60, IsAdvanced = true }
                )
            );

            FactionIconVerticalOffset = _config.Bind<float>(
                "Kill Feed",
                "阵营图标垂直偏移",
                -30f,
                new ConfigDescription(
                    "阵营图标相对于原始布局的垂直偏移（像素），负值向上，正值向下。",
                    new AcceptableValueRange<float>(-200f, 200f),
                    new ConfigurationManagerAttributes { Order = 59, IsAdvanced = true }
                )
            );

            ExperienceTextWidth = _config.Bind<float>(
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
            NameOutlineOpacity = _config.Bind<float>(
                "Kill Feed",
                "玩家名字描边不透明度",
                0.1549296f,
                new ConfigDescription(
                    "玩家名字描边颜色的不透明度（0-1）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 49, Category = "颜色" }
                )
            );

            NameOutlineThickness = _config.Bind<float>(
                "Kill Feed",
                "玩家名字描边厚度",
                1.1f,
                new ConfigDescription(
                    "玩家名字描边的像素厚度",
                    new AcceptableValueRange<float>(0.5f, 10f),
                    new ConfigurationManagerAttributes { Order = 48, Category = "颜色" }
                )
            );

            DebugTriggerKey = _config.Bind(
                "调试",
                "调试触发按键",
                new KeyboardShortcut(KeyCode.P),
                new ConfigDescription(
                    "按下该按键将立即生成一次随机击中反馈与击杀提示",
                    null,
                    new ConfigurationManagerAttributes { Order = 100, Category = "调试" }
                )
            );

            DebugMode = _config.Bind<bool>(
                "调试",
                "启用调试日志",
                false,
                new ConfigDescription(
                    "是否在控制台输出详细的击杀和调试信息（开启可能导致微小卡顿）",
                    null,
                    new ConfigurationManagerAttributes { Order = 90, Category = "调试" }
                )
            );

            // ============ 音频配置 ============
            EnableHitSound = _config.Bind<bool>(
                "音频",
                "启用命中音效",
                true,
                new ConfigDescription(
                    "是否播放命中音效",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }
                )
            );

            EnableKillSound = _config.Bind<bool>(
                "音频",
                "启用击杀音效",
                true,
                new ConfigDescription(
                    "是否播放击杀音效",
                    null,
                    new ConfigurationManagerAttributes { Order = 90 }
                )
            );

            HitSoundVolume = _config.Bind<float>(
                "音频",
                "命中音效音量",
                0.5f,
                new ConfigDescription(
                    "命中音效的音量（0.0 - 1.0）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 80 }
                )
            );

            KillSoundVolume = _config.Bind<float>(
                "音频",
                "击杀音效音量",
                0.7f,
                new ConfigDescription(
                    "击杀音效的音量（0.0 - 1.0）",
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes { Order = 70 }
                )
            );
        }
    }
}
