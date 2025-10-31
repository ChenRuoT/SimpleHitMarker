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

namespace SimpleHitMarker
{
    [BepInPlugin("com.shiunaya.simplehitmarker", "shm", "0.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;
        public static bool hitDetected = false;
        public float hitDuration = 0.5f; // Duration to show the hit marker
        public static Vector3 hitWorldPoint;
        public static float hitTime = 0f;
        public static float hitDamage = 0f; // store last hit damage

        // 暴露静态日志，便于补丁中可靠输出到 BepInEx 控制台/日志
        public static BepInEx.Logging.ManualLogSource Log;

        //贴图与动画
        public static Texture2D hitTexture;
        public float hitBaseSize = 128f; // base pixel size

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

            harmony = new Harmony("com.shiunaya.simplehitmarker");
            harmony.PatchAll();
            Log.LogInfo("SimpleHitMarker Plugin is loaded!");
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
            if (hitTexture != null)
            {
                try { Destroy(hitTexture); } catch { }
                hitTexture = null;
            }
        }

        public void Update()
        {

        }

        void OnGUI()
        {
            if (hitDetected && Time.time - hitTime < hitDuration)
            {
                float t = (Time.time - hitTime) / hitDuration;
                float alpha = 1f - t;
                float scale = Mathf.Lerp(1.4f, 1f, t); // pop animation
                float size = hitBaseSize * scale;

                Color originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

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
                try
                {
                    string dmgText = hitDamage.ToString("0");
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.alignment = TextAnchor.MiddleLeft;
                    style.normal.textColor = new Color(1f, 1f, 1f, alpha);
                    style.fontSize = Mathf.Clamp(Mathf.RoundToInt(size * 0.25f), 12, 48);

                    float padding = 8f;
                    Rect textRect = new Rect(drawRect.xMax + padding, drawRect.center.y - (style.fontSize / 2f), 200f, style.fontSize + 4f);
                    GUI.Label(textRect, dmgText, style);
                }
                catch { }

                GUI.color = originalColor;
            }
            else if (hitDetected)
            {
                hitDetected = false;
            }
        }
    }
}
