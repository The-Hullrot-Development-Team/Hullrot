﻿using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server.RoleTimers
{
    public sealed class RoleTimerSystem : EntitySystem
    {
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        private const int StateCheckTime = 90;
        private Dictionary<NetUserId, CachedPlayerRoleTimers> _cachedPlayerData = new();

        public override void Initialize()
        {
            base.Initialize();
            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
            SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => SaveFullCacheToDb());
        }

        private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            switch (args.NewStatus)
            {
                case SessionStatus.Connecting:
                {
                    await CachePlayerRoles(args.Session.UserId);
                    break;
                }
                case SessionStatus.Disconnected:
                {
                    ClearPlayerFromCache(args.Session.UserId);
                    break;
                }
                case SessionStatus.Zombie:
                    break;
                case SessionStatus.Connected:
                    break;
                case SessionStatus.InGame:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Puts all relevant database information for a player into the cache.
        /// </summary>
        /// <param name="player">The NetUserId representing the player.</param>
        private async Task CachePlayerRoles(NetUserId player)
        {
            var roleTimers = await _db.GetRoleTimers(player);
            var cacheObject = new CachedPlayerRoleTimers();
            foreach (var timer in roleTimers)
            {
                cacheObject.SetCachedPlaytimeForRole(timer.Role, timer.TimeSpent);
            }

            _cachedPlayerData[player] = cacheObject;
        }

        private void ClearPlayerFromCache(NetUserId player)
        {
            if (!_cachedPlayerData.ContainsKey(player)) return;
            SaveCacheDataToDb(player, _cachedPlayerData[player]);
            _cachedPlayerData.Remove(player);
        }

        private void SaveFullCacheToDb()
        {
            foreach (var (user, data) in _cachedPlayerData)
            {
                SaveCacheDataToDb(user, data);
            }
        }

        private async Task SaveCacheDataToDb(NetUserId player, CachedPlayerRoleTimers? data = null)
        {
            var pdata = data ?? _cachedPlayerData[player];
            foreach (var role in pdata.CurrentRoles)
            {
                var timer = await _db.CreateOrGetRoleTimer(player, role);
                await _db.AddRoleTime(timer.Id, DateTime.UtcNow.Subtract(pdata.GetLastSavedTime(role)!.Value));
            }
        }

        /// <summary>
        /// Checks for role changes on the player and saves any roles no longer being tracked.
        /// </summary>
        /// <param name="player">The player to check for role changes.</param>
        /// <param name="mind">A mind to check for roles (pass the player's mind or it'll fuck up)</param>
        /// <param name="roles">A full list of the new roles the player has. If this is passed, mind will be ignored.</param>
        public void PlayerRolesChanged(NetUserId player, Mind.Mind? mind = null, HashSet<string>? roles = null)
        {

        }

        /// <summary>
        /// Gets a list of roles the player doesn't fulfill the requirements for.
        /// </summary>
        /// <param name="id">The player's network id.</param>
        /// <returns>A HashSet of disallowed roles.</returns>
        public HashSet<string>? GetDisallowedRoles(NetUserId id)
        {
            if (!IsPlayerTimeCached(id)) return null;
            // TODO: Disallowed roles logic.
            return new HashSet<string>();
        }

        public bool IsPlayerTimeCached(NetUserId id)
        {
            return _cachedPlayerData.ContainsKey(id);
        }

        /// <summary>
        /// Gets a dictionary of playtime for all roles from the cache.
        /// </summary>
        /// <remarks>
        /// This is not guaranteed to be accurate - cached playtime is only updated when something changes.
        /// </remarks>
        /// <param name="id">The player's ID</param>
        public Dictionary<string, TimeSpan>? GetCachedRoleTimersForPlayer(NetUserId id)
        {
            if (!IsPlayerTimeCached(id)) return null;
            var dict = new Dictionary<string, TimeSpan>();
            foreach (var (role, (lastSaved, playtime)) in _cachedPlayerData[id].GetAllRoleTimers())
            {
                dict.Add(role, playtime);
            }

            return dict;
        }
    }

    /// <summary>
    /// A dictionary of cached role timers, including the last time they were saved, and the time spent playing them
    /// as well as a HashSet of the roles they're currently playing.
    /// </summary>
    public struct CachedPlayerRoleTimers
    {
        public HashSet<string> CurrentRoles;
        // The reasoning for having a DateTime here is that we don't need to update it, and
        // can instead just figure out how much time has passed since they first joined and now,
        // and use that to get the TimeSpan to add onto the saved playtime
        private Dictionary<string, Tuple<DateTime, TimeSpan>> _roleTimers;

        public TimeSpan? GetPlaytimeForRole(string role)
        {
            if (!_roleTimers.ContainsKey(role)) return null;
            return _roleTimers[role].Item2;
        }

        public Dictionary<string, Tuple<DateTime, TimeSpan>> GetAllRoleTimers()
        {
            return _roleTimers;
        }

        /// <summary>
        /// Sets the cached playtime for a specific role.
        /// Doesn't change anything on the database, only information in the cache.
        /// </summary>
        /// <remarks>
        /// Updates the "last saved" datetime object as well.
        /// </remarks>
        /// <param name="role">The role ID to alter.</param>
        /// <param name="time">The duration of time played.</param>
        public void SetCachedPlaytimeForRole(string role, TimeSpan time)
        {
            DateTime lastSaved;
            lastSaved = _roleTimers.ContainsKey(role) ? _roleTimers[role].Item1 : DateTime.UtcNow;
            _roleTimers[role] = new Tuple<DateTime, TimeSpan>(lastSaved, time);
        }

        public DateTime? GetLastSavedTime(string role)
        {
            if (!_roleTimers.ContainsKey(role)) return null;
            return _roleTimers[role].Item1;
        }
    }
}
