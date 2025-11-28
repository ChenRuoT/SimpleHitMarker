using System.Collections.Generic;
using UnityEngine;
using SimpleHitMarker;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// 击杀提示UI管理器
    /// </summary>
    public class KillFeedUI
    {
        
        // 当前显示的击杀信息列表
        private List<ActiveKillDisplay> activeKills = new List<ActiveKillDisplay>();
        
        // 骷髅头纹理
        public Texture2D SkullTexture { get; set; }
        public Texture2D RedSkullTexture { get; set; }
        
        /// <summary>
        /// 添加新的击杀提示
        /// </summary>
        public void AddKill(KillInfo killInfo)
        {
            // 计算连杀数
            int killStreak = CalculateKillStreak(killInfo.KillTime);
            killInfo.KillStreak = killStreak;
            
            // 计算新骷髅头的起始位置（根据连杀数）
            float startX = Plugin.KillFeedStartX?.Value ?? 50f;
            float skullSpacing = Plugin.SkullSpacing?.Value ?? 60f;
            float skullStartX = startX;
            
            if (killStreak > 1)
            {
                // 如果有连杀，新骷髅头应该显示在右侧
                // 之前的骷髅头需要向左推
                PushPreviousSkulls();
                // 新骷髅头的位置根据连杀数计算
                skullStartX = startX + ((killStreak - 1) * skullSpacing);
            }
            
            // 创建新的显示项
            var display = new ActiveKillDisplay
            {
                KillInfo = killInfo,
                StartTime = Time.time,
                SkullStartX = skullStartX,
                SkullCurrentX = skullStartX
            };
            
            activeKills.Add(display);
        }
        
        /// <summary>
        /// 计算连杀数
        /// </summary>
        private int CalculateKillStreak(float currentKillTime)
        {
            int streak = 1;
            float streakWindow = Plugin.StreakWindow?.Value ?? 10f;
            
            foreach (var kill in activeKills)
            {
                if (currentKillTime - kill.KillInfo.KillTime <= streakWindow)
                {
                    streak = Mathf.Max(streak, kill.KillInfo.KillStreak + 1);
                }
            }
            
            return streak;
        }
        
        /// <summary>
        /// 向左推挤之前的骷髅头
        /// </summary>
        private void PushPreviousSkulls()
        {
            float skullDisplayDuration = Plugin.SkullDisplayDuration?.Value ?? 2f;
            float skullSpacing = Plugin.SkullSpacing?.Value ?? 60f;
            
            foreach (var existing in activeKills)
            {
                // 如果这个击杀还在显示骷髅头，则向左推
                float elapsed = Time.time - existing.StartTime;
                if (elapsed < skullDisplayDuration)
                {
                    existing.SkullStartX -= skullSpacing;
                    existing.SkullCurrentX -= skullSpacing;
                }
            }
        }
        
        /// <summary>
        /// 更新UI（每帧调用）
        /// </summary>
        public void Update()
        {
            float currentTime = Time.time;
            float killFeedDuration = Plugin.KillFeedDuration?.Value ?? 5f;
            float animationSpeed = Plugin.SkullAnimationSpeed?.Value ?? 5f;
            
            // 移除过期的击杀提示
            activeKills.RemoveAll(kill => currentTime - kill.StartTime > killFeedDuration);
            
            // 更新骷髅头位置动画
            foreach (var kill in activeKills)
            {
                // 平滑移动到目标位置
                if (Mathf.Abs(kill.SkullCurrentX - kill.SkullStartX) > 0.1f)
                {
                    kill.SkullCurrentX = Mathf.Lerp(kill.SkullCurrentX, kill.SkullStartX, Time.deltaTime * animationSpeed);
                }
            }
        }
        
        /// <summary>
        /// 绘制UI（在OnGUI中调用）
        /// </summary>
        public void OnGUI()
        {
            if (activeKills.Count == 0) return;
            
            float currentTime = Time.time;
            float killFeedDuration = Plugin.KillFeedDuration?.Value ?? 5f;
            
            foreach (var kill in activeKills)
            {
                float elapsed = currentTime - kill.StartTime;
                if (elapsed > killFeedDuration) continue;
                
                DrawKillFeed(kill, elapsed);
            }
        }
        
        /// <summary>
        /// 绘制单个击杀提示
        /// </summary>
        private void DrawKillFeed(ActiveKillDisplay kill, float elapsed)
        {
            var info = kill.KillInfo;
            float killFeedDuration = Plugin.KillFeedDuration?.Value ?? 5f;
            float alpha = Mathf.Clamp01(1f - (elapsed / killFeedDuration));
            
            float startX = Plugin.KillFeedStartX?.Value ?? 50f;
            float startY = Plugin.KillFeedStartY?.Value ?? 100f;
            float lineHeight = Plugin.KillFeedLineHeight?.Value ?? 30f;
            float lineSpacing = Plugin.KillFeedLineSpacing?.Value ?? 8f;
            
            float yPos = startY;
            
            // 第一行：阵营图标和等级/阵营名
            DrawFactionLine(info, yPos, alpha, startX, lineHeight);
            yPos += lineHeight + lineSpacing;
            
            // 第二行：骷髅头（splash效果）和经验值
            DrawSkullLine(kill, info, yPos, elapsed, alpha, lineHeight);
            yPos += lineHeight + lineSpacing;
            
            // 第三行：玩家名称
            DrawPlayerNameLine(info, yPos, alpha, startX, lineHeight);
            yPos += lineHeight + lineSpacing;
            
            // 第四行：击杀信息（部位/方式/距离）
            DrawKillDetailsLine(info, yPos, alpha, startX, lineHeight);
        }
        
        /// <summary>
        /// 绘制第一行：阵营图标和等级/阵营名
        /// </summary>
        private void DrawFactionLine(KillInfo info, float yPos, float alpha, float startX, float lineHeight)
        {
            Color factionColor = Plugin.ColorFaction?.Value ?? Color.white;
            int fontSize = Plugin.FontSizeFaction?.Value ?? 16;
            float iconSize = Plugin.FactionIconSize?.Value ?? 24f;
            
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = new Color(factionColor.r, factionColor.g, factionColor.b, alpha);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            
            float xPos = startX;
            
            // 绘制阵营图标（如果有）
            if (info.FactionIcon != null)
            {
                Rect iconRect = new Rect(xPos, yPos, iconSize, iconSize);
                Color originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(iconRect, info.FactionIcon);
                GUI.color = originalColor;
                xPos += iconSize + 6f;
            }
            
            // 绘制等级和阵营名
            string factionText = $"[{info.PlayerLevel}] {info.Faction}";
            Rect textRect = new Rect(xPos, yPos, 300f, lineHeight);
            GUI.Label(textRect, factionText, style);
        }
        
        /// <summary>
        /// 绘制第二行：骷髅头和经验值
        /// </summary>
        private void DrawSkullLine(ActiveKillDisplay kill, KillInfo info, float yPos, float elapsed, float alpha, float lineHeight)
        {
            float skullDisplayDuration = Plugin.SkullDisplayDuration?.Value ?? 2f;
            float skullFadeDuration = Plugin.SkullFadeDuration?.Value ?? 0.3f;
            float skullSize = Plugin.SkullSize?.Value ?? 64f;
            Color expColor = Plugin.ColorExperience?.Value ?? new Color(1f, 1f, 0.8f, 1f);
            int expFontSize = Plugin.FontSizeExperience?.Value ?? 20;
            float expTextWidth = Plugin.ExperienceTextWidth?.Value ?? 180f;
            
            // 计算骷髅头的显示状态
            bool showSkull = elapsed < skullDisplayDuration;
            float skullAlpha = showSkull ? 1f : 0f;
            
            // 如果正在淡出
            if (elapsed >= skullDisplayDuration - skullFadeDuration && elapsed < skullDisplayDuration)
            {
                float fadeProgress = (elapsed - (skullDisplayDuration - skullFadeDuration)) / skullFadeDuration;
                skullAlpha = 1f - fadeProgress;
            }
            
            // 绘制骷髅头（每个击杀显示一个，连杀时位置会向左推）
            Texture2D skullTex = info.IsHeadshot ? RedSkullTexture : SkullTexture;
            if (skullTex != null && showSkull)
            {
                // 只绘制当前击杀的骷髅头
                Rect skullRect = new Rect(kill.SkullCurrentX, yPos, skullSize, skullSize);
                
                Color originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, skullAlpha * alpha);
                GUI.DrawTexture(skullRect, skullTex);
                GUI.color = originalColor;
            }
            
            // 绘制经验值（右侧）
            GUIStyle expStyle = new GUIStyle(GUI.skin.label);
            expStyle.normal.textColor = new Color(expColor.r, expColor.g, expColor.b, alpha);
            expStyle.fontSize = expFontSize;
            expStyle.fontStyle = FontStyle.Bold;
            expStyle.alignment = TextAnchor.MiddleRight;
            
            string expText = $"+{info.Experience}";
            Rect expRect = new Rect(Screen.width - (expTextWidth + 20f), yPos, expTextWidth, lineHeight);
            GUI.Label(expRect, expText, expStyle);
        }
        
        /// <summary>
        /// 绘制第三行：玩家名称
        /// </summary>
        private void DrawPlayerNameLine(KillInfo info, float yPos, float alpha, float startX, float lineHeight)
        {
            Color nameColor = Plugin.ColorPlayerName?.Value ?? new Color(1f, 0.3f, 0.3f, 1f);
            int fontSize = Plugin.FontSizePlayerName?.Value ?? 18;
            
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = new Color(nameColor.r, nameColor.g, nameColor.b, alpha);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
            
            Rect nameRect = new Rect(startX, yPos, 400f, lineHeight);
            GUI.Label(nameRect, info.PlayerName ?? "Unknown", style);
        }
        
        /// <summary>
        /// 绘制第四行：击杀详情
        /// </summary>
        private void DrawKillDetailsLine(KillInfo info, float yPos, float alpha, float startX, float lineHeight)
        {
            Color detailsColor = Plugin.ColorKillDetails?.Value ?? new Color(0.8f, 0.8f, 0.8f, 1f);
            int fontSize = Plugin.FontSizeKillDetails?.Value ?? 14;
            
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = new Color(detailsColor.r, detailsColor.g, detailsColor.b, alpha);
            style.fontSize = fontSize;
            
            // 构建详情文本
            List<string> details = new List<string>();
            
            if (info.IsHeadshot)
            {
                details.Add("爆头");
            }
            else if (info.BodyPart != EBodyPart.Head)
            {
                // 如果不是爆头，显示身体部位（避免重复显示头部）
                details.Add(GetBodyPartName(info.BodyPart));
            }
            
            if (info.Distance > 0)
            {
                details.Add($"{info.Distance:F0}m");
            }
            
            if (!string.IsNullOrEmpty(info.KillMethod))
            {
                details.Add(info.KillMethod);
            }
            
            string detailsText = string.Join(" · ", details);
            
            Rect detailsRect = new Rect(startX, yPos, 400f, lineHeight);
            GUI.Label(detailsRect, detailsText, style);
        }
        
        /// <summary>
        /// 获取身体部位的中文名称
        /// </summary>
        private string GetBodyPartName(EBodyPart bodyPart)
        {
            switch (bodyPart)
            {
                case EBodyPart.Head: return "头部";
                case EBodyPart.Chest: return "胸部";
                case EBodyPart.Stomach: return "腹部";
                case EBodyPart.LeftArm: return "左臂";
                case EBodyPart.RightArm: return "右臂";
                case EBodyPart.LeftLeg: return "左腿";
                case EBodyPart.RightLeg: return "右腿";
                default: return bodyPart.ToString();
            }
        }
        
        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            activeKills.Clear();
        }
    }
    
    /// <summary>
    /// 活跃的击杀显示项
    /// </summary>
    internal class ActiveKillDisplay
    {
        public KillInfo KillInfo { get; set; }
        public float StartTime { get; set; }
        public float SkullStartX { get; set; }
        public float SkullCurrentX { get; set; }
    }
}

