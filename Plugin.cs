using BepInEx;
using BepInEx.Logging;
using EFT;
using HarmonyLib;
using SimpleHitMarker.KillFeed;
using SimpleHitmarker.KillPatch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SimpleHitMarker
{
    [BepInPlugin("com.shiunaya.simplehitmarker", "SimpleHitMarker", "0.1.1")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static new ManualLogSource Log { get; private set; } // Hide base Logger to allow static access if needed, or just assign to it

        public ConfigurationManager ConfigManager { get; private set; }
        public AudioManager Audio { get; private set; }
        public DamageIndicatorUI DamageUI { get; private set; }
        public KillFeedUI KillFeedUI { get; private set; }

        private Harmony harmony;

        // PMC Rank Icons
        private const int MaxPmcRankIconLevel = 79;
        private static readonly Dictionary<int, Texture2D> PmcRankIcons = new Dictionary<int, Texture2D>();
        private static readonly int[] PmcRankTierStarts = { 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75 };
        private static readonly HashSet<string> PmcBotTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "USEC", "BEAR" };

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Initialize Managers
            ConfigManager = new ConfigurationManager(Config);
            DamageUI = new DamageIndicatorUI(ConfigManager, Log);
            Audio = new AudioManager(ConfigManager, Log);
            KillFeedUI = new KillFeedUI(ConfigManager);

            // Load Resources for KillFeed (Textures that were previously loaded in Plugin)
            LoadKillFeedResources();
            LoadPmcRankIcons();

            // Patching
            harmony = new Harmony("com.shiunaya.simplehitmarker");
            harmony.PatchAll();

            // Subscribe to Events
            KillEventManager.Subscribe();

            Log.LogInfo("SimpleHitMarker Plugin is loaded!");
        }

        private void LoadKillFeedResources()
        {
            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

                string skullPath = Path.Combine(assemblyDir, "SimpleHitMarker", "skull.png");
                Texture2D skull = TextureLoader.LoadTextureFromFile(skullPath);
                if (skull == null)
                {
                    Log.LogWarning("[SimpleHitMarker] skull.png not found. Kill feed skull will not display.");
                }
                KillFeedUI.SkullTexture = skull;

                string redSkullPath = Path.Combine(assemblyDir, "SimpleHitMarker", "skull_head.png");
                Texture2D redSkull = TextureLoader.LoadTextureFromFile(redSkullPath);
                if (redSkull == null)
                {
                    redSkull = skull;
                    Log.LogInfo("[SimpleHitMarker] skull_head.png not found. Using regular skull for headshots.");
                }
                KillFeedUI.RedSkullTexture = redSkull;
            }
            catch (Exception ex)
            {
                Log.LogError($"[SimpleHitMarker] Skull texture load error: {ex}");
            }
        }

        private void OnDestroy()
        {
            KillEventManager.Unsubscribe();
            harmony.UnpatchSelf();

            DamageUI?.Cleanup();
            KillFeedUI?.Cleanup();

            // Clean up Rank Icons
            foreach (var texture in PmcRankIcons.Values)
            {
                if (texture != null) Destroy(texture);
            }
            PmcRankIcons.Clear();
        }

        private float _lastAudioCheckTime = 0f;
        private const float AudioCheckInterval = 5f; // Check every 5 seconds

        private void Update()
        {
            HandleDebugInput();
            KillFeedUI?.Update();

            // Proactively ensure AudioSource is ready, especially after scene transitions
            if (Time.time - _lastAudioCheckTime > AudioCheckInterval)
            {
                _lastAudioCheckTime = Time.time;
                Audio?.CheckAndRestoreSource();
            }
        }

        private void OnGUI()
        {
            DamageUI?.OnGUI();
            KillFeedUI?.OnGUI();
        }

        // Exposed for Patches
        public void RegisterDamageEvent(float damage, Vector3 position, bool isHeadshot)
        {
            DamageUI?.RegisterHit(damage, isHeadshot);
            Audio?.PlayHitSound(isHeadshot);
        }

        public void PlayKillSound(bool isHeadshot)
        {
            Audio?.PlayKillSound(isHeadshot);
        }

        // ================== Resource Loading & Helpers ==================

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
                        if (!File.Exists(candidatePath)) continue;

                        iconTexture = TextureLoader.LoadTextureFromFile(candidatePath);
                        if (iconTexture != null) break;
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

        public Texture2D GetPmcRankIcon(string botType, int playerLevel)
        {
            if (!IsPmcBotType(botType) || playerLevel <= 0) return null;

            int tierKey = ResolvePmcRankTierStart(playerLevel);
            if (tierKey <= 0) return null;

            if (PmcRankIcons.TryGetValue(tierKey, out var icon)) return icon;
            return null;
        }

        private static bool IsPmcBotType(string botType)
        {
            return !string.IsNullOrWhiteSpace(botType) && PmcBotTypes.Contains(botType);
        }

        private static int ResolvePmcRankTierStart(int playerLevel)
        {
            if (playerLevel <= 0) return -1;
            if (playerLevel <= 4) return 1;
            int clamped = Mathf.Clamp(playerLevel, 5, MaxPmcRankIconLevel);
            int steps = (clamped - 5) / 5;
            int tierStart = 5 + (steps * 5);
            return Mathf.Clamp(tierStart, 5, 75);
        }

        // ================== Debug Logic ==================

        private void HandleDebugInput()
        {
            if (ConfigManager?.DebugTriggerKey == null) return;

            var shortcut = ConfigManager.DebugTriggerKey.Value;
            if (shortcut.IsDown())
            {
                GenerateDebugHit();
                GenerateDebugKill();
            }
        }

        private static readonly System.Random DebugRandom = new System.Random();
        private static readonly string[] DebugNames = { "Tigris", "Northwind", "KappaFox", "Windrunner", "NightOwl", "Skyline" };
        private static readonly string[] DebugKillMethods = { "M4A1", "AK-105", "MP7A2", "SR-25", "SV-98", "UMP-45", "MK17" };

        private void GenerateDebugHit()
        {
            bool isHeadshot = DebugRandom.NextDouble() > 0.7;
            float damage = Mathf.Round(RandomRange(25f, 120f));
            RegisterDamageEvent(damage, Vector3.zero, isHeadshot);
        }

        private void GenerateDebugKill()
        {
            if (KillFeedUI == null) return;

            bool isHeadshot = DebugRandom.NextDouble() > 0.6;
            bool isPmc = DebugRandom.NextDouble() > 0.4;
            int level = DebugRandom.Next(1, 70);

            EBodyPart[] parts = { EBodyPart.Head, EBodyPart.Chest, EBodyPart.Stomach, EBodyPart.LeftArm, EBodyPart.RightLeg };
            EBodyPart bodyPart = isHeadshot ? EBodyPart.Head : PickRandom(parts);

            WildSpawnType role = isPmc
                ? (DebugRandom.NextDouble() > 0.5 ? WildSpawnType.pmcUSEC : WildSpawnType.pmcBEAR)
                : WildSpawnType.assault;

            string faction = isPmc ? (role == WildSpawnType.pmcUSEC ? "USEC" : "BEAR") : "Scav";

            var killInfo = new KillInfo
            {
                PlayerName = PickRandom(DebugNames) + (isPmc ? "" : " (Scav)"),
                PlayerLevel = level,
                Role = role,
                Faction = faction,
                KillTime = Time.time,
                BodyPart = bodyPart,
                IsHeadshot = isHeadshot,
                KillMethod = PickRandom(DebugKillMethods),
                Distance = RandomRange(5f, 300f),
                Experience = 250 + (isHeadshot ? 50 : 0) + (DebugRandom.Next(0, 5) * 200)
            };

            string botType = BotTypeMapping.GetBotType(role);
            killInfo.FactionIcon = GetPmcRankIcon(botType, level);

            KillFeedUI.AddKill(killInfo);
            PlayKillSound(isHeadshot);
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)DebugRandom.NextDouble() * (max - min);
        }

        private T PickRandom<T>(T[] array)
        {
            if (array == null || array.Length == 0) return default;
            return array[DebugRandom.Next(array.Length)];
        }
    }
}
