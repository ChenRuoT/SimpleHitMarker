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
        // 记录最后造成伤害的信息，用于击杀检测
        public static DamageInfoStruct? LastDamageInfo { get; private set; }
        public static EBodyPart LastBodyPart { get; private set; }
        public static Player LastVictim { get; private set; }
        
        static void Postfix(Player __instance, DamageInfoStruct damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, float absorbed)
        {
            // ���������ÿ�� ApplyDamageInfo ������ʱ������¼һ�Σ�����ȷ�� Harmony �����Ƿ񴥷���
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

                Plugin.Log.LogInfo($"[SimpleHitMarker] ApplyDamageInfo called. Damage={damageInfo.Damage}, Aggressor={aggressorId}, Body={bodyPartType}, Collider={colliderType}, Absorbed={absorbed}, HitPoint={damageInfo.HitPoint}");
            }
            catch (Exception ex)
            {
                // ���ⲹ������־������Ϸ
                try { Plugin.Log.LogError($"[SimpleHitMarker] ApplyDamageInfo logging error: {ex}"); } catch { }
            }

            // ԭ���߼��������У����ı䣩��
            // ȷ���й�������Ϣ
            IPlayerOwner aggressor = damageInfo.Player;
            if (aggressor?.iPlayer == null) return;

            // ֻ����������Ϊ������ҵ���������ǿ�ǹ����ˣ�
            if (!aggressor.iPlayer.IsYourPlayer) return;

            // ���ˣ���Ч�˺� + ���� + ��λ
            //if (damageInfo.Damage <= 5f) return;

            Player localPlayer = aggressor.iPlayer as Player;
            if (localPlayer == null) return;

            float distance = Vector3.Distance(localPlayer.Position, __instance.Position);
            //if (distance < 5f || distance > 100f) return;

            //if (bodyPartType != EBodyPart.Head && bodyPartType != EBodyPart.Chest) return;

            bool isHeadshot = bodyPartType == EBodyPart.Head;
            Plugin.RegisterDamageEvent(damageInfo.Damage, damageInfo.HitPoint, isHeadshot);
            
            // 记录最后伤害信息，用于击杀检测
            LastDamageInfo = damageInfo;
            LastBodyPart = bodyPartType;
            LastVictim = __instance;

            Plugin.Log.LogInfo($"[SimpleHitMarker] Hit detected. Point={damageInfo.HitPoint}, Damage={damageInfo.Damage}, Body={bodyPartType}, Distance={distance}");
        }
    }
}