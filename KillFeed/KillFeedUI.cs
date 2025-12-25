using System.Collections.Generic;
using UnityEngine;
using SimpleHitMarker;
using SimpleHitMarker.Localization;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// 击杀提示UI管理器
    /// </summary>
    public class KillFeedUI
    {
        private readonly ConfigurationManager _config;

        // 当前显示的击杀信息列表
        private List<ActiveKillDisplay> activeKills = new List<ActiveKillDisplay>();

        // 骷髅头纹理
        public Texture2D SkullTexture { get; set; }
        public Texture2D RedSkullTexture { get; set; }

        public KillFeedUI(ConfigurationManager config)
        {
            _config = config;
        }

        private float GetAnchorX()
        {
            float offset = _config.KillFeedHorizontalOffset?.Value ?? 220f;
            return (Screen.width * 0.5f) - offset;
        }

        private float GetBlockWidth()
        {
            float width = _config.KillFeedBlockWidth?.Value ?? 420f;
            return Mathf.Max(100f, width);
        }

        private float GetVerticalCenter()
        {
            float verticalOffset = _config.KillFeedVerticalOffset?.Value ?? 0f;
            return (Screen.height * 0.5f) + verticalOffset;
        }

        /// <summary>
        /// 添加新的击杀提示
        /// </summary>
        public void AddKill(KillInfo killInfo)
        {
            // 计算连杀数
            int killStreak = CalculateKillStreak(killInfo.KillTime);
            killInfo.KillStreak = killStreak;

            if (killStreak > 1)
            {
                PushPreviousSkulls();
            }

            // 创建新的显示项
            var display = new ActiveKillDisplay
            {
                KillInfo = killInfo,
                StartTime = Time.time,
                SkullTargetOffset = 0f,
                SkullCurrentOffset = 0f
            };

            activeKills.Add(display);
        }

        /// <summary>
        /// 计算连杀数
        /// </summary>
        private int CalculateKillStreak(float currentKillTime)
        {
            int streak = 1;
            float streakWindow = _config.StreakWindow?.Value ?? 10f;

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
            float skullDisplayDuration = _config.SkullDisplayDuration?.Value ?? 2f;
            float skullSpacing = _config.SkullSpacing?.Value ?? 60f;
            float skullSize = _config.SkullSize?.Value ?? 64f;
            float shift = Mathf.Max(0f, skullSize + skullSpacing);

            foreach (var existing in activeKills)
            {
                // 如果这个击杀还在显示骷髅头，则向左推
                float elapsed = Time.time - existing.StartTime;
                if (elapsed < skullDisplayDuration)
                {
                    existing.SkullTargetOffset += shift;
                }
            }
        }

        /// <summary>
        /// 更新UI（每帧调用）
        /// </summary>
        public void Update()
        {
            float currentTime = Time.time;
            float killFeedDuration = _config.KillFeedDuration?.Value ?? 5f;
            float skullDisplayDuration = _config.SkullDisplayDuration?.Value ?? 2f;
            float animationSpeed = _config.SkullPushAnimationSpeed?.Value
                ?? _config.SkullAnimationSpeed?.Value
                ?? 5f;
            float removalThreshold = Mathf.Max(killFeedDuration, skullDisplayDuration);

            // 移除过期的击杀提示
            activeKills.RemoveAll(kill => currentTime - kill.StartTime > removalThreshold);

            // 更新骷髅头位置动画
            foreach (var kill in activeKills)
            {
                // 平滑移动到目标位置
                if (Mathf.Abs(kill.SkullCurrentOffset - kill.SkullTargetOffset) > 0.1f)
                {
                    kill.SkullCurrentOffset = Mathf.Lerp(
                        kill.SkullCurrentOffset,
                        kill.SkullTargetOffset,
                        Time.deltaTime * animationSpeed
                    );
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
            float killFeedDuration = _config.KillFeedDuration?.Value ?? 5f;
            float skullDisplayDuration = _config.SkullDisplayDuration?.Value ?? 2f;

            List<ActiveKillDisplay> textKills = null;
            List<ActiveKillDisplay> skullKills = null;
            foreach (var kill in activeKills)
            {
                float elapsed = currentTime - kill.StartTime;

                if (elapsed <= killFeedDuration)
                {
                    textKills ??= new List<ActiveKillDisplay>();
                    textKills.Add(kill);
                }

                if (elapsed <= skullDisplayDuration)
                {
                    skullKills ??= new List<ActiveKillDisplay>();
                    skullKills.Add(kill);
                }
            }

            if (skullKills == null && textKills == null)
            {
                return;
            }

            // Compute a dynamic line height: base it on max of faction font, name font, details font and skull size
            float skullSize = _config.SkullSize?.Value ?? 64f;
            int fontFaction = _config.FontSizeFaction?.Value ?? 16;
            int fontName = _config.FontSizePlayerName?.Value ?? 18;
            int fontDetails = _config.FontSizeKillDetails?.Value ?? 14;
            // approximate text height as font size * some factor (1.2) to allow for padding
            float textHeightsMax = Mathf.Max(fontFaction, fontName, fontDetails) * 1.2f;
            float lineHeight = Mathf.Clamp(Mathf.Max(textHeightsMax, skullSize * 0.5f), 14f, 200f);
            float lineSpacing = _config.KillFeedLineSpacing?.Value ?? 8f;
            float anchorX = GetAnchorX();
            float anchorY = GetVerticalCenter();
            float blockWidth = GetBlockWidth();

            if (skullKills != null)
            {
                DrawSkullIcons(skullKills, anchorX, anchorY, currentTime);
            }

            if (textKills == null || textKills.Count == 0)
            {
                return;
            }

            ActiveKillDisplay latestKill = textKills[textKills.Count - 1];
            float latestElapsed = currentTime - latestKill.StartTime;
            float textAlpha = Mathf.Clamp01(1f - (latestElapsed / killFeedDuration));

            // position top row above the skull icons with spacing
            float topRowY = anchorY - ((_config.SkullSize?.Value ?? 64f) * 0.5f) - lineSpacing - lineHeight;
            DrawFactionLine(latestKill.KillInfo, topRowY, textAlpha, anchorX, lineHeight, blockWidth);

            DrawExperienceText(latestKill.KillInfo, anchorX, anchorY, textAlpha, lineHeight);

            float playerNameY = anchorY + ((_config.SkullSize?.Value ?? 64f) * 0.5f) + lineSpacing;
            DrawPlayerNameLine(latestKill.KillInfo, playerNameY, textAlpha, anchorX, lineHeight, blockWidth);

            float detailsY = playerNameY + lineHeight + lineSpacing;
            DrawKillDetailsLine(latestKill.KillInfo, detailsY, textAlpha, anchorX, lineHeight, blockWidth);
        }

        /// <summary>
        /// 绘制第一行：阵营图标和等级/阵营名
        /// </summary>
        private void DrawFactionLine(KillInfo info, float yPos, float alpha, float anchorX, float lineHeight, float blockWidth)
        {
            Color factionColor = _config.ColorFaction?.Value ?? Color.white;
            int fontSize = _config.FontSizeFaction?.Value ?? 16;
            float iconSize = _config.FactionIconSize?.Value ?? 24f;

            // Ensure faction line is positioned to avoid vertical overlap with skull icon
            // Compute anchor and skull parameters to determine minimal top Y for the faction line
            float anchorY = GetVerticalCenter();
            float skullSize = _config.SkullSize?.Value ?? 64f;
            float fixedSeparation = 10f;
            float dynamicSeparation = Mathf.Max(6f, skullSize * 0.2f, iconSize * 0.15f);

            float skullTopY = anchorY - (skullSize * 0.5f);
            float maxTopY = skullTopY - fixedSeparation - dynamicSeparation - lineHeight;

            // yPos 越小越靠上，限制不要低于 maxTopY（即放到骷髅上方）
            yPos = Mathf.Min(yPos, maxTopY);
            yPos = Mathf.Clamp(yPos, 2f, Screen.height - lineHeight - 2f);

            GUIStyle baseStyle = new GUIStyle(GUI.skin.label);
            baseStyle.fontSize = fontSize;
            baseStyle.fontStyle = FontStyle.Bold;
            baseStyle.alignment = TextAnchor.MiddleRight;

            float rowRight = anchorX;
            const float iconPadding = 6f;

            // 绘制阵营图标（如果有）
            // vertical offset from config (negative = up, positive = down)
            float factionIconVerticalOffset = _config.FactionIconVerticalOffset?.Value ?? -20f;
            float iconY = yPos + Mathf.Max(0f, (lineHeight - iconSize) * 0.5f) + factionIconVerticalOffset;
            if (info.FactionIcon != null)
            {
                Rect iconRect = new Rect(rowRight - iconSize, iconY, iconSize, iconSize);
                Color originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(iconRect, info.FactionIcon);
                GUI.color = originalColor;
                rowRight -= iconSize + iconPadding;
            }

            // 绘制等级和Bot类型，垂直方向居中对齐到阵营图标
            string botType = BotTypeMapping.GetBotType(info.Role);

            bool showLevel = false;
            string levelText = "";
            if (!string.IsNullOrEmpty(botType))
            {
                if (string.Equals(botType, "USEC", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(botType, "BEAR", System.StringComparison.OrdinalIgnoreCase))
                {
                    showLevel = true;
                    levelText = $"[{info.PlayerLevel}] ";
                }
            }

            string botText = string.IsNullOrEmpty(botType) ? "Unknown" : botType;

            // Prepare styles
            GUIStyle levelStyle = new GUIStyle(baseStyle);
            levelStyle.normal.textColor = new Color(factionColor.r, factionColor.g, factionColor.b, alpha);

            // Determine bot-type color from Plugin config (fallback to factionColor)
            Color botColor = factionColor;
            string bt = (botText ?? string.Empty).ToUpperInvariant();
            if (bt == "USEC") botColor = _config.ColorUSEC?.Value ?? botColor;
            else if (bt == "BEAR") botColor = _config.ColorBEAR?.Value ?? botColor;
            else if (bt == "SCAV" || bt == "SCAVENGER") botColor = _config.ColorSCAV?.Value ?? botColor;
            else if (bt == "BOSS") botColor = _config.ColorBOSS?.Value ?? botColor;
            else if (bt == "FOLLOWER" || bt == "FOLLOWERS") botColor = _config.ColorFollower?.Value ?? botColor;
            else if (bt == "RAIDER") botColor = _config.ColorRAIDER?.Value ?? botColor;
            else if (bt == "ROUGES" || bt == "ROUGE" || bt == "ROGUE") botColor = _config.ColorRouges?.Value ?? botColor;
            else if (bt == "SECTANT") botColor = _config.ColorSectant?.Value ?? botColor;

            GUIStyle botStyle = new GUIStyle(baseStyle);
            botStyle.normal.textColor = new Color(botColor.r, botColor.g, botColor.b, alpha);

            // Compose GUIContent for measuring
            GUIContent levelContent = new GUIContent(showLevel ? levelText : string.Empty);
            GUIContent botContent = new GUIContent(botText);

            Vector2 levelSize = levelStyle.CalcSize(levelContent);
            Vector2 botSize = botStyle.CalcSize(botContent);

            float spacing = 2f;
            float totalWidth = levelSize.x + botSize.x + (showLevel ? spacing : 0f);
            totalWidth = Mathf.Min(totalWidth, blockWidth);

            float startX = rowRight - totalWidth;
            float textY = (iconY + (iconSize * 0.5f)) - (lineHeight * 0.5f);

            // Draw level (if any)
            if (showLevel)
            {
                Rect levelRect = new Rect(startX, textY, levelSize.x, lineHeight);
                GUI.Label(levelRect, levelContent, levelStyle);
            }

            // Draw bot type
            Rect botRect = new Rect(startX + (showLevel ? levelSize.x + spacing : 0f), textY, botSize.x, lineHeight);
            GUI.Label(botRect, botContent, botStyle);
        }

        /// <summary>
        /// 绘制骷髅头图标（居中）
        /// </summary>
        private void DrawSkullIcons(
            List<ActiveKillDisplay> kills,
            float anchorX,
            float anchorY,
            float currentTime)
        {
            float skullDisplayDuration = _config.SkullDisplayDuration?.Value ?? 2f;
            float skullFadeDuration = _config.SkullFadeDuration?.Value ?? 0.3f;
            float skullSize = _config.SkullSize?.Value ?? 64f;

            foreach (var kill in kills)
            {
                float elapsed = currentTime - kill.StartTime;
                if (elapsed >= skullDisplayDuration)
                {
                    continue;
                }

                Texture2D skullTex = kill.KillInfo.IsHeadshot ? RedSkullTexture : SkullTexture;
                if (skullTex == null)
                {
                    continue;
                }

                float skullAlpha = 1f;
                if (elapsed >= skullDisplayDuration - skullFadeDuration)
                {
                    float fadeProgress = (elapsed - (skullDisplayDuration - skullFadeDuration)) / skullFadeDuration;
                    skullAlpha = Mathf.Clamp01(1f - fadeProgress);
                }

                float skullX = anchorX - skullSize - kill.SkullCurrentOffset;
                float skullY = anchorY - (skullSize * 0.5f);
                Rect skullRect = new Rect(skullX, skullY, skullSize, skullSize);

                Color originalColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, skullAlpha);
                GUI.DrawTexture(skullRect, skullTex);
                GUI.color = originalColor;
            }
        }

        /// <summary>
        /// 绘制经验值文本（位于锚点右侧）
        /// </summary>
        private void DrawExperienceText(KillInfo info, float anchorX, float anchorY, float alpha, float lineHeight)
        {
            Color expColor = _config.ColorExperience?.Value ?? new Color(1f, 1f, 0.8f, 1f);
            int expFontSize = _config.FontSizeExperience?.Value ?? 20;
            float expTextWidth = _config.ExperienceTextWidth?.Value ?? 180f;
            float expOffset = _config.KillFeedExperienceHorizontalOffset?.Value ?? 40f;

            GUIStyle expStyle = new GUIStyle(GUI.skin.label);
            expStyle.normal.textColor = new Color(expColor.r, expColor.g, expColor.b, alpha);
            expStyle.fontSize = expFontSize;
            expStyle.fontStyle = FontStyle.Bold;
            expStyle.alignment = TextAnchor.MiddleLeft;

            string expText = $"{info.Experience}";
            Rect expRect = new Rect(anchorX + expOffset, anchorY - (lineHeight * 0.5f), expTextWidth, lineHeight);
            GUI.Label(expRect, expText, expStyle);
        }

        /// <summary>
        /// 绘制第三行：玩家名称
        /// </summary>
        private void DrawPlayerNameLine(KillInfo info, float yPos, float alpha, float anchorX, float lineHeight, float blockWidth)
        {
            Color nameColor = _config.ColorPlayerName?.Value ?? new Color(1f, 0.3f, 0.3f, 1f);
            int fontSize = _config.FontSizePlayerName?.Value ?? 18;

            GUIStyle fillStyle = new GUIStyle(GUI.skin.label);
            fillStyle.normal.textColor = new Color(nameColor.r, nameColor.g, nameColor.b, alpha);
            fillStyle.fontSize = fontSize;
            fillStyle.fontStyle = FontStyle.Bold;
            fillStyle.alignment = TextAnchor.MiddleRight;

            GUIStyle outlineStyle = new GUIStyle(fillStyle);

            // Use dedicated name outline config (same color as name)
            float outlineAlpha = Mathf.Clamp01(_config.NameOutlineOpacity?.Value ?? 1f);
            float outlineThickness = Mathf.Clamp(_config.NameOutlineThickness?.Value ?? 1.5f, 0.5f, 10f);

            Color outlineColor = new Color(nameColor.r, nameColor.g, nameColor.b, nameColor.a);
            outlineColor.a *= outlineAlpha * alpha; // respect both outline opacity config and overall alpha
            outlineStyle.normal.textColor = outlineColor;

            Rect nameRect = new Rect(anchorX - blockWidth, yPos, blockWidth, lineHeight);
            GUIContent content = new GUIContent(info.PlayerName ?? "Unknown");

            DamageIndicatorUI.DrawOutlinedLabel(nameRect, content, fillStyle, outlineStyle, outlineThickness);
        }

        /// <summary>
        /// 绘制第四行：击杀详情
        /// </summary>
        private void DrawKillDetailsLine(KillInfo info, float yPos, float alpha, float anchorX, float lineHeight, float blockWidth)
        {
            Color detailsColor = _config.ColorKillDetails?.Value ?? new Color(0.8f, 0.8f, 0.8f, 1f);
            int fontSize = _config.FontSizeKillDetails?.Value ?? 14;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = new Color(detailsColor.r, detailsColor.g, detailsColor.b, alpha);
            style.fontSize = fontSize;
            style.alignment = TextAnchor.MiddleRight;

            // 构建详情文本
            List<string> details = new List<string>();

            if (info.IsHeadshot)
            {
                details.Add("爆头");
            }
            else if (info.BodyPart != EBodyPart.Head)
            {
                // 如果不是爆头，显示身体部位（避免重复显示头部）
                details.Add("部位:" + GetBodyPartName(info.BodyPart));
            }

            if (info.Distance > 0)
            {
                details.Add($"距离:{info.Distance:F0}m");
            }

            if (!string.IsNullOrEmpty(info.KillMethod))
            {
                details.Add("武器:" + info.KillMethod);
            }

            string detailsText = string.Join(" · ", details);

            Rect detailsRect = new Rect(anchorX - blockWidth, yPos, blockWidth, lineHeight);
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
        /// 获取角色显示名称（优先使用本地化后的 WildSpawnType）
        /// </summary>
        private string GetRoleDisplayName(KillInfo info)
        {
            if (info == null)
            {
                return "Unknown";
            }

            string roleKey = $"WildSpawnType/{info.Role}";
            string localizedRole = LocalizedHelper.Localized(roleKey);
            if (!string.IsNullOrWhiteSpace(localizedRole) && !string.Equals(localizedRole, roleKey, System.StringComparison.Ordinal))
            {
                return localizedRole;
            }

            if (!string.IsNullOrWhiteSpace(info.Faction))
            {
                return info.Faction;
            }

            return info.Role.ToString();
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
        public float SkullTargetOffset { get; set; }
        public float SkullCurrentOffset { get; set; }
    }
}
