using EFT;
using Comfort.Common;
using System;
using UnityEngine;
using SimpleHitMarker;
using SimpleHitMarker.KillFeed;

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
                Plugin.Log.LogInfo("[shm] Kill event subscribed successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[shm] Failed to subscribe kill event: {ex}");
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
                Plugin.Log.LogInfo("[shm] Kill event unsubscribed");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[shm] Failed to unsubscribe kill event: {ex}");
            }
        }
        
        /// <summary>
        /// 玩家死亡事件处理
        /// 事件签名：Action<Player, IPlayer, DamageInfoStruct, EBodyPart>
        /// </summary>
        private static void OnPlayerKilled(Player deadPlayer, IPlayer killer, DamageInfoStruct damageInfo, EBodyPart bodyPart)
        {
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
                if (Plugin.KillFeedUI != null)
                {
                    Plugin.KillFeedUI.AddKill(killInfo);
                }
                
                Plugin.Log.LogInfo($"[shm] Kill detected: {killInfo.PlayerName} (Level {killInfo.PlayerLevel}) by {killer.Profile?.Nickname}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[shm] Kill event handler error: {ex}");
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
                killInfo.PlayerName = victim.Profile?.Info?.Nickname ?? "Unknown";
                killInfo.PlayerLevel = victim.Profile?.Info?.Level ?? 1;
                
                // 获取阵营
                EPlayerSide side = victim.Side;
                killInfo.Faction = GetFactionName(side);
                
                // 获取角色类型（PMC/Scav等）
                var role = victim.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[shm] Error getting player info: {ex}");
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
                if (damageInfo.Weapon != null)
                {
                    killInfo.KillMethod = damageInfo.Weapon.ShortName ?? "Unknown";
                }
                else
                {
                    killInfo.KillMethod = damageInfo.DamageType.ToString();
                }
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
        /// 获取阵营名称
        /// </summary>
        private static string GetFactionName(EPlayerSide side)
        {
            switch (side)
            {
                case EPlayerSide.Usec:
                    return "USEC";
                case EPlayerSide.Bear:
                    return "BEAR";
                case EPlayerSide.Savage:
                    return "Scav";
                default:
                    return "Unknown";
            }
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
    }
}

