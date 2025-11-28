using UnityEngine;
using EFT;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// 击杀信息数据结构
    /// </summary>
    public class KillInfo
    {
        /// <summary>
        /// 被击杀的玩家
        /// </summary>
        public Player Victim { get; set; }
        
        /// <summary>
        /// 击杀者（本地玩家）
        /// </summary>
        public IPlayer Killer { get; set; }
        
        /// <summary>
        /// 被击杀的部位
        /// </summary>
        public EBodyPart BodyPart { get; set; }
        
        /// <summary>
        /// 是否为爆头击杀
        /// </summary>
        public bool IsHeadshot { get; set; }
        
        /// <summary>
        /// 击杀距离（米）
        /// </summary>
        public float Distance { get; set; }
        
        /// <summary>
        /// 击杀经验值
        /// </summary>
        public int Experience { get; set; }
        
        /// <summary>
        /// 击杀时间
        /// </summary>
        public float KillTime { get; set; }
        
        /// <summary>
        /// 连杀数（从1开始）
        /// </summary>
        public int KillStreak { get; set; }
        
        /// <summary>
        /// 击杀方式（武器名称等）
        /// </summary>
        public string KillMethod { get; set; }
        
        /// <summary>
        /// 玩家名称
        /// </summary>
        public string PlayerName { get; set; }
        
        /// <summary>
        /// 玩家等级
        /// </summary>
        public int PlayerLevel { get; set; }
        
        /// <summary>
        /// 阵营（USEC/BEAR/Scav等）
        /// </summary>
        public string Faction { get; set; }

        /// <summary>
        /// 角色类型（WildSpawnType）
        /// </summary>
        public WildSpawnType Role { get; set; }
        
        /// <summary>
        /// 阵营图标纹理（如果有）
        /// </summary>
        public Texture2D FactionIcon { get; set; }
    }
}

