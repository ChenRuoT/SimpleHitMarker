using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

namespace SimpleHitMarker.Patches
{
    /// <summary>
    /// “土办法”补丁：监听控制台输出，当观察到特定信号时触发音频重载。
    /// 信号："[Info   :ModulePatch] Cached ai brain weights in client"
    /// </summary>
    public static class LogInterceptorPatch
    {
        private static bool _hasTriggeredInThisSession = false;

        public static void Enable()
        {
            var harmony = new Harmony("com.shiunaya.simplehitmarker.loginterceptor");

            // 我们 Hook ManualLogSource 的 Log 方法
            var original = typeof(ManualLogSource).GetMethod("Log", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(LogLevel), typeof(object) }, null);
            var prefix = typeof(LogInterceptorPatch).GetMethod(nameof(LogPrefix), BindingFlags.Static | BindingFlags.NonPublic);

            if (original != null && prefix != null)
            {
                harmony.Patch(original, new HarmonyMethod(prefix));
                Plugin.Log.LogInfo("[SimpleHitMarker] LogInterceptor enabled - listening for AI brain weights signal...");
            }
        }

        private static void LogPrefix(object data)
        {
            if (data == null) return;

            string message = data.ToString();

            // 匹配用户观察到的特定字符串
            if (message.Contains("Cached ai brain weights in client"))
            {
                // 为了防止在同一个加载阶段多次触发，可以加个简单的逻辑（或者不加，因为 CheckAndRestoreSource 本身很轻）
                Plugin.Log.LogInfo("[SimpleHitMarker] Signal detected! Proactively refreshing audio system...");

                if (Plugin.Instance != null && Plugin.Instance.Audio != null)
                {
                    Plugin.Instance.Audio.CheckAndRestoreSource();

                    // 触发一次调试UI显示，作为"欢迎仪式"并确认Audio/UI已加载
                    Plugin.Instance.GenerateDebugHit();
                    Plugin.Instance.GenerateDebugKill(false);
                    Plugin.Log.LogInfo("[SimpleHitMarker] Welcome shot fired!");
                }
            }
        }
    }
}
