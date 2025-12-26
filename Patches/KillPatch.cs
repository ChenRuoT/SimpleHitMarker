using EFT;
using Comfort.Common;
using System;
using UnityEngine;
using SimpleHitMarker;
using SimpleHitMarker.KillFeed;
using SimpleHitMarker.Localization;

namespace SimpleHitmarker.KillPatch
{
    /// <summary>
    /// 击杀检测事件处理器
    /// 使用 Player.OnPlayerDeadStatic 事件来检测击杀
    /// </summary>
    public class KillEventManager
    {
        /// <summary>
        /// 订阅击杀事件
        /// </summary>
        public static void Subscribe()
        {
            try
            {
                Player.OnPlayerDeadStatic += OnPlayerKilled;
                Plugin.Log.LogInfo("[SimpleHitMarker] Kill event subscribed successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SimpleHitMarker] Failed to subscribe kill event: {ex}");
            }
        }

        /// <summary>
        /// 取消订阅击杀事件
        /// </summary>
        public static void Unsubscribe()
        {
            try
            {
                Player.OnPlayerDeadStatic -= OnPlayerKilled;
                Plugin.Log.LogInfo("[SimpleHitMarker] Kill event unsubscribed");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SimpleHitMarker] Failed to unsubscribe kill event: {ex}");
            }
        }

        /// <summary>
        /// 玩家死亡事件处理
        /// 事件签名：Action<Player, IPlayer, DamageInfoStruct, EBodyPart>
        /// </summary>
        private static void OnPlayerKilled(Player deadPlayer, IPlayer killer, DamageInfoStruct damageInfo, EBodyPart bodyPart)
        {
            if (Plugin.Instance?.ConfigManager?.DebugMode?.Value == true)
            {
                LogKillEventRaw(deadPlayer, killer, damageInfo, bodyPart);
            }

            try
            {
                // 检查是否是本地玩家击杀的（使用 IsYourPlayer 避免 MainPlayer 依赖）
                if (killer == null || !killer.IsYourPlayer)
                {
                    return; // 不是本地玩家的击杀
                }

                // 创建击杀信息
                var killInfo = CreateKillInfo(deadPlayer, killer, damageInfo, bodyPart);

                // 添加到击杀提示系统
                if (Plugin.Instance.KillFeedUI != null)
                {
                    Plugin.Instance.KillFeedUI.AddKill(killInfo);
                }

                // 播放击杀音效
                bool isHeadshotKill = bodyPart == EBodyPart.Head;
                Plugin.Instance.PlayKillSound(isHeadshotKill);

                if (Plugin.Instance?.ConfigManager?.DebugMode?.Value == true)
                {
                    LogKillInfoDetails(killInfo, killer, damageInfo);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SimpleHitMarker] Kill event handler error: {ex}");
            }
        }

        /// <summary>
        /// 创建击杀信息
        /// </summary>
        private static KillInfo CreateKillInfo(Player victim, IPlayer killer, DamageInfoStruct damageInfo, EBodyPart bodyPart)
        {
            var killInfo = new KillInfo
            {
                Victim = victim,
                Killer = killer,
                KillTime = Time.time,
                BodyPart = bodyPart,
                IsHeadshot = bodyPart == EBodyPart.Head
            };

            // 获取玩家信息
            try
            {
                string localizedName = LocalizedHelper.Transliterate(victim.Profile?.Info?.Nickname);
                killInfo.PlayerName = string.IsNullOrWhiteSpace(localizedName) ? "Unknown" : localizedName;
                killInfo.PlayerLevel = victim.Profile?.Info?.Level ?? 1;

                // 获取角色类型（PMC/Scav等）
                var role = victim.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;
                killInfo.Role = role;

                string botType = BotTypeMapping.GetBotType(role);
                killInfo.FactionIcon = Plugin.Instance.GetPmcRankIcon(botType, killInfo.PlayerLevel);

                // 获取阵营（优先角色显示）
                EPlayerSide side = victim.Side;
                killInfo.Faction = GetRoleDisplayName(role, side);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[SimpleHitMarker] Error getting player info: {ex}");
            }

            // 计算距离
            try
            {
                killInfo.Distance = Vector3.Distance(killer.Position, victim.Position);
            }
            catch { }

            // 获取武器信息
            try
            {
                killInfo.KillMethod = GetWeaponName(damageInfo);
            }
            catch
            {
                killInfo.KillMethod = damageInfo.DamageType.ToString();
            }

            // 计算经验值（可以根据游戏规则调整）
            killInfo.Experience = CalculateExperience(killInfo);

            return killInfo;
        }

        /// <summary>
        /// 获取角色或阵营名称（优先角色本地化）
        /// </summary>
        private static string GetRoleDisplayName(WildSpawnType role, EPlayerSide fallbackSide)
        {
            string roleKey = $"WildSpawnType/{role}";
            string localizedRole = LocalizedHelper.Localized(roleKey);
            if (!string.IsNullOrWhiteSpace(localizedRole) &&
                !string.Equals(localizedRole, roleKey, StringComparison.Ordinal))
            {
                return localizedRole;
            }

            return GetFactionName(fallbackSide);
        }

        /// <summary>
        /// 获取阵营名称（Side）
        /// </summary>
        private static string GetFactionName(EPlayerSide side)
        {
            string key = $"EPlayerSide/{side}";
            string localized = LocalizedHelper.LocalizedEnum(side);
            if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, key, StringComparison.Ordinal))
            {
                return localized;
            }

            return side.ToString();
        }

        /// <summary>
        /// 计算经验值
        /// 注意：这里使用估算值，实际经验值应该从游戏统计系统获取
        /// </summary>
        private static int CalculateExperience(KillInfo killInfo)
        {
            int baseExp = 100;

            if (killInfo.IsHeadshot)
            {
                baseExp += 50;
            }

            // 可以根据距离、连杀等调整
            if (killInfo.KillStreak > 1)
            {
                baseExp += killInfo.KillStreak * 25;
            }

            return baseExp;
        }

        private static string GetWeaponName(DamageInfoStruct damageInfo)
        {
            if (damageInfo.Weapon != null)
            {
                string shortNameKey = damageInfo.Weapon.ShortName;
                string localizedShort = LocalizedHelper.Localized(shortNameKey);
                if (!string.IsNullOrWhiteSpace(localizedShort) && !string.Equals(localizedShort, shortNameKey, StringComparison.Ordinal))
                {
                    return localizedShort;
                }

                string longNameKey = damageInfo.Weapon.Name;
                string localizedLong = LocalizedHelper.Localized(longNameKey);
                if (!string.IsNullOrWhiteSpace(localizedLong) && !string.Equals(localizedLong, longNameKey, StringComparison.Ordinal))
                {
                    return localizedLong;
                }

                return shortNameKey ?? longNameKey ?? damageInfo.Weapon.TemplateId.ToString();
            }

            return damageInfo.DamageType.ToString();
        }

        private static void LogKillEventRaw(Player deadPlayer, IPlayer killer, DamageInfoStruct damageInfo, EBodyPart bodyPart)
        {
            if (Plugin.Log == null)
            {
                return;
            }

            try
            {
                string victimName = deadPlayer?.Profile?.Info?.Nickname ?? "Unknown";
                string killerName = killer?.Profile?.Nickname ?? "Unknown";
                string weaponName = damageInfo.Weapon?.ShortName ?? damageInfo.Weapon?.Name ?? damageInfo.DamageType.ToString();
                float distance = -1f;
                try
                {
                    if (deadPlayer != null && killer != null)
                    {
                        distance = Vector3.Distance(deadPlayer.Position, killer.Position);
                    }
                }
                catch
                {
                    // ignored, best effort only
                }

                string distanceText = distance >= 0f ? distance.ToString("0.0") : "n/a";
                Plugin.Log.LogInfo($"[SimpleHitMarker] Kill event fired. Victim={victimName}, Killer={killerName}, KillerIsLocal={killer?.IsYourPlayer ?? false}, Body={bodyPart}, Damage={damageInfo.Damage}, DamageType={damageInfo.DamageType}, Weapon={weaponName}, Distance={distanceText}");
            }
            catch (Exception ex)
            {
                try { Plugin.Log.LogError($"[SimpleHitMarker] Kill event logging error: {ex}"); } catch { }
            }
        }

        private static void LogKillInfoDetails(KillInfo killInfo, IPlayer killer, DamageInfoStruct damageInfo)
        {
            if (Plugin.Log == null || killInfo == null)
            {
                return;
            }

            try
            {
                string killerName = killer?.Profile?.Nickname ?? "Unknown";
                string botType = BotTypeMapping.GetBotType(killInfo.Role);
                string localizedRole = string.IsNullOrWhiteSpace(killInfo.Faction)
                    ? killInfo.Role.ToString()
                    : killInfo.Faction;

                Plugin.Log.LogInfo(
                    $"[SimpleHitMarker] Kill info => Victim={killInfo.PlayerName} Lv{killInfo.PlayerLevel} ({localizedRole}) Role={killInfo.Role}, BotType={botType}, Killer={killerName}, Body={killInfo.BodyPart}, Headshot={killInfo.IsHeadshot}, Dist={killInfo.Distance:0.0}, Weapon={killInfo.KillMethod}, Exp={killInfo.Experience}, Streak={killInfo.KillStreak}, Damage={damageInfo.Damage}, DamageType={damageInfo.DamageType}"
                );
            }
            catch (Exception ex)
            {
                try { Plugin.Log.LogError($"[SimpleHitMarker] Kill info logging error: {ex}"); } catch { }
            }
        }
    }
}

