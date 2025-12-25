using EFT;
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
        public static void OnBeingHit(DamageInfoStruct damageInfo, EBodyPart bodyPart, float absorbed)
        {
            try
            {
                // 获取攻击者信息
                IPlayerOwner aggressor = damageInfo.Player;
                if (aggressor?.iPlayer == null) return;

                // 只有攻击者是本地玩家时才处理
                if (!aggressor.iPlayer.IsYourPlayer) return;

                // 注册命中事件（显示 UI 和播放音效）
                bool isHeadshot = bodyPart == EBodyPart.Head;
                Plugin.Instance.RegisterDamageEvent(damageInfo.Damage, damageInfo.HitPoint, isHeadshot);

                // 记录详细信息用于调试
                Plugin.Log.LogInfo($"[SimpleHitMarker] Hit detected (Event). Point={damageInfo.HitPoint}, Damage={damageInfo.Damage}, Body={bodyPart}, Absorbed={absorbed}");
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
