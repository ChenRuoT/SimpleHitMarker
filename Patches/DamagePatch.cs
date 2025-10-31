using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;
using SimpleHitMarker;

namespace SimpleHitmarker.DamagePatch
{
    [HarmonyPatch(typeof(Player), "ApplyDamageInfo")]
    public class DamagePatch
    {
        static void Postfix(Player __instance, DamageInfoStruct damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, float absorbed)
        {
            // 调试输出：每次 ApplyDamageInfo 被调用时立即记录一次（用于确认 Harmony 补丁是否触发）
            try
            {
                string aggressorId = "null";
                try
                {
                    if (damageInfo.Player?.iPlayer != null)
                    {
                        aggressorId = damageInfo.Player.iPlayer.ProfileId?.ToString() ?? "(no profile)";
                    }
                }
                catch { /* ignore nested retrieval errors */ }

                Plugin.Log.LogInfo($"[shm] ApplyDamageInfo called. Damage={damageInfo.Damage}, Aggressor={aggressorId}, Body={bodyPartType}, Collider={colliderType}, Absorbed={absorbed}, HitPoint={damageInfo.HitPoint}");
            }
            catch (Exception ex)
            {
                // 避免补丁因日志崩溃游戏
                try { Plugin.Log.LogError($"[shm] ApplyDamageInfo logging error: {ex}"); } catch { }
            }

            // 原有逻辑继续运行（不改变）。
            // 确保有攻击者信息
            IPlayerOwner aggressor = damageInfo.Player;
            if (aggressor?.iPlayer == null) return;

            // 只处理攻击者为本地玩家的情况（我们开枪打别人）
            if (!aggressor.iPlayer.IsYourPlayer) return;

            // 过滤：有效伤害 + 距离 + 体位
            //if (damageInfo.Damage <= 5f) return;

            Player localPlayer = aggressor.iPlayer as Player;
            if (localPlayer == null) return;

            float distance = Vector3.Distance(localPlayer.Position, __instance.Position);
            //if (distance < 5f || distance > 100f) return;

            //if (bodyPartType != EBodyPart.Head && bodyPartType != EBodyPart.Chest) return;

            // 触发 + 存储 HitPoint
            Plugin.hitDetected = true;
            Plugin.hitTime = Time.time;
            Plugin.hitWorldPoint = damageInfo.HitPoint;
            Plugin.hitDamage = damageInfo.Damage;

            Plugin.Log.LogInfo($"[shm] Hit detected. Point={damageInfo.HitPoint}, Damage={damageInfo.Damage}, Body={bodyPartType}, Distance={distance}");
        }
    }
}