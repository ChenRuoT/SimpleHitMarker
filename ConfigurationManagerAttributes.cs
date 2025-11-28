using System;
using BepInEx.Configuration;

namespace SimpleHitMarker
{
    /// <summary>
    /// ConfigurationManager属性类，用于控制配置项在ConfigurationManager中的显示方式
    /// </summary>
    public class ConfigurationManagerAttributes
    {
        public int? Order { get; set; } = null;
        public bool? IsAdvanced { get; set; } = null;
        public string Category { get; set; } = null;
        public Action<ConfigEntryBase> CustomDrawer { get; set; } = null;
        public bool? HideSettingName { get; set; } = null;
        public bool? Browsable { get; set; } = null;
        public object DefaultValue { get; set; } = null;
        public bool? ReadOnly { get; set; } = null;
        public bool? ShowRangeAsPercent { get; set; } = null;
        public string Description { get; set; } = null;
        public string DispName { get; set; } = null;
        public bool? HideDefaultButton { get; set; } = null;
    }
}

