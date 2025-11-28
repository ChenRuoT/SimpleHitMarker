using System.Collections.Generic;
using EFT;

namespace SimpleHitMarker.KillFeed
{
    /// <summary>
    /// 映射 WildSpawnType 到自定义的 Bot 类型/名称。
    /// 默认值先使用枚举名，后续可按需改成更友好的显示文本。
    /// </summary>
    internal static class BotTypeMapping
    {
        private static readonly Dictionary<WildSpawnType, string> Map = new Dictionary<WildSpawnType, string>
        {
            // 常见通用分类：SCAV, USEC, BEAR, PMC, BOSS, Follower, Infected, Raider, Spirit
            { WildSpawnType.marksman, "SCAV" },
            { WildSpawnType.assault, "SCAV" },
            { WildSpawnType.bossTest, "BOSS" },
            { WildSpawnType.bossBully, "BOSS" },
            { WildSpawnType.followerTest, "Follower" },
            { WildSpawnType.followerBully, "Follower" },
            { WildSpawnType.bossKilla, "BOSS" },
            { WildSpawnType.bossKojaniy, "BOSS" },
            { WildSpawnType.followerKojaniy, "Follower" },
            { WildSpawnType.pmcBot, "Raider" },
            { WildSpawnType.cursedAssault, "SCAV" },
            { WildSpawnType.bossGluhar, "BOSS" },
            { WildSpawnType.followerGluharAssault, "Follower" },
            { WildSpawnType.followerGluharSecurity, "Follower" },
            { WildSpawnType.followerGluharScout, "Follower" },
            { WildSpawnType.followerGluharSnipe, "Follower" },
            { WildSpawnType.followerSanitar, "Follower" },
            { WildSpawnType.bossSanitar, "BOSS" },
            { WildSpawnType.test, "SCAV" },
            { WildSpawnType.assaultGroup, "SCAV" },
            { WildSpawnType.sectantWarrior, "Sectant" },
            { WildSpawnType.sectantPriest, "Sectant" },
            { WildSpawnType.bossTagilla, "BOSS" },
            { WildSpawnType.followerTagilla, "Follower" },
            { WildSpawnType.exUsec, "Rouges" },
            { WildSpawnType.gifter, "SCAV" },
            { WildSpawnType.bossKnight, "BOSS" },
            { WildSpawnType.followerBigPipe, "BOSS" },
            { WildSpawnType.followerBirdEye, "BOSS" },
            { WildSpawnType.bossZryachiy, "BOSS" },
            { WildSpawnType.followerZryachiy, "Follower" },
            { WildSpawnType.bossBoar, "BOSS" },
            { WildSpawnType.followerBoar, "Follower" },
            { WildSpawnType.arenaFighter, "Raider" },
            { WildSpawnType.arenaFighterEvent, "Raider" },
            { WildSpawnType.bossBoarSniper, "BOSS" },
            { WildSpawnType.crazyAssaultEvent, "SCAV" },
            { WildSpawnType.peacefullZryachiyEvent, "BOSS" },
            { WildSpawnType.sectactPriestEvent, "Sectant" },
            { WildSpawnType.ravangeZryachiyEvent, "BOSS" },
            { WildSpawnType.followerBoarClose1, "Follower" },
            { WildSpawnType.followerBoarClose2, "Follower" },
            { WildSpawnType.bossKolontay, "BOSS" },
            { WildSpawnType.followerKolontayAssault, "Follower" },
            { WildSpawnType.followerKolontaySecurity, "Follower" },
            { WildSpawnType.shooterBTR, "SCAV" },
            { WildSpawnType.bossPartisan, "BOSS" },
            { WildSpawnType.spiritWinter, "Spirit" },
            { WildSpawnType.spiritSpring, "Spirit" },
            { WildSpawnType.peacemaker, "SCAV" },
            { WildSpawnType.pmcBEAR, "BEAR" },
            { WildSpawnType.pmcUSEC, "USEC" },
            { WildSpawnType.skier, "SCAV" },
            { WildSpawnType.sectantPredvestnik, "Sectant" },
            { WildSpawnType.sectantPrizrak, "Sectant" },
            { WildSpawnType.sectantOni, "Sectant" },
            { WildSpawnType.infectedAssault, "Infected" },
            { WildSpawnType.infectedPmc, "Infected" },
            { WildSpawnType.infectedCivil, "Infected" },
            { WildSpawnType.infectedLaborant, "Infected" },
            { WildSpawnType.infectedTagilla, "Infected" },
            { WildSpawnType.bossTagillaAgro, "BOSS" },
            { WildSpawnType.bossKillaAgro, "BOSS" },
            { WildSpawnType.tagillaHelperAgro, "SCAV" },
        };

        /// <summary>
        /// 获取当前 role 对应的 Bot 类型显示名称（bottype）。
        /// </summary>
        public static string GetBotType(WildSpawnType role)
        {
            if (Map.TryGetValue(role, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return role.ToString();
        }
    }
}


