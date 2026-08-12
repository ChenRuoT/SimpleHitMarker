using EFT;
using EFT.Ballistics;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using UnityEngine;
using SimpleHitMarker;

namespace SimpleHitmarker.DamagePatch
{
    /// <summary>
    /// 伤害检测事件处理器
    /// 通过模块补丁订阅每个玩家实例的 BeingHitAction 事件
    /// </summary>
    public class DamageEventManager : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 补丁 Player.Init 以便在玩家初始化时订阅事件
            return AccessTools.Method(typeof(Player), nameof(Player.Init));
        }

        [PatchPostfix]
        public static void Postfix(Player __instance)
        {
            if (__instance == null) return;

            // 订阅伤害命中事件
            __instance.BeingHitAction += OnBeingHit;
        }

        /// <summary>
        /// 伤害命中事件处理逻辑
        /// </summary>
        public static void OnBeingHit(DamageInfo damageInfo, EBodyPart bodyPart, float absorbed)
        {
            try
            {
                // 获取攻击者信息
                IObserverToPlayerBridge aggressor = damageInfo.Player;
                if (aggressor?.iPlayer == null) return;

                // 只有攻击者是本地玩家时才处理
                if (!aggressor.iPlayer.IsYourPlayer) return;

                // 每颗子弹只会触发一次 BeingHitAction（Player.ApplyShot 依次调用
                // ProceedLocalAbsorbedDamage 设置 DidArmorDamage、再调用 ApplyDamageInfo
                // 设置 DidBodyDamage 并触发事件），因此不存在需要去重的重复事件。
                // 旧逻辑在“护甲和身体同时受伤”时 return，恰好丢弃了击中有甲目标这一最常见的情况
                // （例如爆头戴头盔的 PMC），导致致命一击不显示命中标记。
                // 这里只跳过完全没有造成任何伤害的事件。
                if (damageInfo.DidArmorDamage <= 0.01f && damageInfo.DidBodyDamage <= 0.01f) return;

                // 注册命中事件（显示 UI 和播放音效）
                bool isHeadshot = bodyPart == EBodyPart.Head;
                Plugin.Instance.RegisterDamageEvent(damageInfo.DidBodyDamage, damageInfo.HitPoint, isHeadshot);

                // 记录详细信息用于调试
                if (Plugin.Instance?.ConfigManager?.DebugMode?.Value == true)
                {
                    Plugin.Log.LogInfo($"[SimpleHitMarker] Hit detected (Event). Point={damageInfo.HitPoint}, Damage={damageInfo.Damage}, Body={bodyPart}, Absorbed={absorbed}");
                    Plugin.Log.LogDebug($"[SimpleHitMarker] Hit Damage Further Info. DamageType={damageInfo.DamageType}, PenetrationPower={damageInfo.PenetrationPower}, ArmorDa" +
                        $"mage={damageInfo.ArmorDamage}, IsForwardHit={damageInfo.IsForwardHit}, HBleeding={damageInfo.HeavyBleedingDelta}, LBleeding={damageInfo.LightBleedingDelta}" +
                        $", DidBodyDamage={damageInfo.DidBodyDamage}, DidArmorDamage={damageInfo.DidArmorDamage}, Penetrated={damageInfo.Penetrated}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[SimpleHitMarker] Damage event handler error: {ex}");
            }
        }
    }

    /// <summary>
    /// 玩家销毁时的反订阅补丁
    /// </summary>
    public class DamageUnsubscribePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 补丁 Player.OnDestroy 以便在玩家销毁时取消订阅
            return AccessTools.Method(typeof(Player), "OnDestroy");
        }

        [PatchPrefix]
        public static void Prefix(Player __instance)
        {
            if (__instance == null) return;

            // 取消订阅命中事件，防止内存泄漏
            __instance.BeingHitAction -= DamageEventManager.OnBeingHit;
        }
    }
}
