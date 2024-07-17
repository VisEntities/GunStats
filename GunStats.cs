using Newtonsoft.Json;
using System.Collections.Generic;

/*
 * Originally created by Orange, up to version 1.0.5
 * Rewritten from scratch and maintained to present by VisEntities
 */

namespace Oxide.Plugins
{
    [Info("Gun Stats", "VisEntities", "2.2.0")]
    [Description("Tracks your weapon use history, including kills and hits, and displays it on the weapon name.")]

    public class GunStats : RustPlugin
    {
        #region Fields

        private static GunStats _plugin;
        private static Configuration _config;
        private Dictionary<ulong, WeaponStats> _weaponStats = new Dictionary<ulong, WeaponStats>();

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Weapon Name Format")]
            public string WeaponNameFormat { get; set; }

            [JsonProperty("Include NPC In Stats")]
            public bool IncludeNPCInStats { get; set; }

            [JsonProperty("Excluded Item Short Names")] 
            public List<string> ExcludedItemShortNames { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            if (string.Compare(_config.Version, "2.1.0") < 0)
            {
                _config.IncludeNPCInStats = defaultConfig.IncludeNPCInStats;
            }

            if (string.Compare(_config.Version, "2.2.0") < 0)
            {
                _config.ExcludedItemShortNames = defaultConfig.ExcludedItemShortNames;
            }

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                WeaponNameFormat = "{weaponName}\nKills: {kills}, Hits: {shotsHit}, Shots: {shotsFired}",
                IncludeNPCInStats = false,
                ExcludedItemShortNames = new List<string>()
                {
                    "explosive.timed",
                    "explosive.satchel",
                    "grenade.f1",
                    "grenade.beancan"
                }
            };
        }

        #endregion Configuration

        #region Data

        private class WeaponStats
        {
            public int Kills { get; set; }
            public int ShotsFired { get; set; }
            public int ShotsHit { get; set; }
        }

        #endregion Data

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private void OnPlayerDeath(BasePlayer victim, HitInfo hitInfo)
        {
            if (hitInfo == null || victim == null)
                return;

            BasePlayer killer = hitInfo.InitiatorPlayer;
            if (killer == null || killer.IsNpc)
                return;

            if (!PermissionUtil.VerifyHasPermission(killer))
                return;

            if (killer == victim)
                return;

            Item item = killer.GetActiveItem();
            if (item == null || _config.ExcludedItemShortNames.Contains(item.info.shortname))
                return;

            if (!_config.IncludeNPCInStats && victim.IsNpc)
                return;

            UpdateWeaponStats(item, addKills: 1);
        }

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (projectile == null || player == null)
                return;

            if (!PermissionUtil.VerifyHasPermission(player))
                return;

            Item item = projectile.GetItem();
            if (item == null || _config.ExcludedItemShortNames.Contains(item.info.shortname))
                return;

            UpdateWeaponStats(item, addShotsFired: 1);
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
        {
            if (hitInfo == null || player == null)
                return;

            BasePlayer killer = hitInfo.InitiatorPlayer;
            if (killer == null || killer.IsNpc)
                return;

            if (!PermissionUtil.VerifyHasPermission(killer))
                return;

            if (killer == player)
                return;

            Item item = killer.GetActiveItem();
            if (item == null || _config.ExcludedItemShortNames.Contains(item.info.shortname))
                return;

            if (!_config.IncludeNPCInStats && player.IsNpc)
                return;

            UpdateWeaponStats(item, addShotsHit: 1);
        }

        #endregion Oxide Hooks

        #region Functions

        private void UpdateWeaponStats(Item weapon, int addKills = 0, int addShotsHit = 0, int addShotsFired = 0)
        {
            WeaponStats stats;
            if (!_weaponStats.TryGetValue(weapon.uid.Value, out stats))
            {
                stats = new WeaponStats();
                _weaponStats[weapon.uid.Value] = stats;
            }

            stats.Kills += addKills;
            stats.ShotsHit += addShotsHit;
            stats.ShotsFired += addShotsFired;

            UpdateWeaponName(weapon, stats);
        }

        private void UpdateWeaponName(Item weapon, WeaponStats stats)
        {
            string formattedName = _config.WeaponNameFormat
                .Replace("{weaponName}", weapon.info.displayName.english)
                .Replace("{kills}", stats.Kills.ToString())
                .Replace("{shotsFired}", stats.ShotsFired.ToString())
                .Replace("{shotsHit}", stats.ShotsHit.ToString());

            weapon.name = formattedName;
            weapon.MarkDirty();
        }

        #endregion Functions

        #region Utility Classes

        private static class PermissionUtil
        {
            public const string USE = "gunstats.use";

            public static void RegisterPermissions()
            {
                _plugin.permission.RegisterPermission(USE, _plugin);
            }

            public static bool VerifyHasPermission(BasePlayer player, string permissionName = USE)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Utility Classes
    }
}
