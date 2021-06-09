using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

#nullable enable

namespace Content.Server.Preferences.Managers
{
    /// <summary>
    /// Sends <see cref="MsgPreferencesAndSettings"/> before the client joins the lobby.
    /// Receives <see cref="MsgSelectCharacter"/> and <see cref="MsgUpdateCharacter"/> at any time.
    /// </summary>
    public class ServerPreferencesManager : IServerPreferencesManager
    {
        [Dependency] private readonly IServerNetManager _netManager = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly IServerDbManager _db = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;

        // Cache player prefs on the server so we don't need as much async hell related to them.
        private readonly Dictionary<NetUserId, PlayerPrefData> _cachedPlayerPrefs =
            new();

        private int MaxCharacterSlots => _cfg.GetCVar(CCVars.GameMaxCharacterSlots);

        public void Init()
        {
            _netManager.RegisterNetMessage<MsgPreferencesAndSettings>(nameof(MsgPreferencesAndSettings));
            _netManager.RegisterNetMessage<MsgSelectCharacter>(nameof(MsgSelectCharacter),
                HandleSelectCharacterMessage);
            _netManager.RegisterNetMessage<MsgUpdateCharacter>(nameof(MsgUpdateCharacter),
                HandleUpdateCharacterMessage);
            _netManager.RegisterNetMessage<MsgDeleteCharacter>(nameof(MsgDeleteCharacter),
                HandleDeleteCharacterMessage);
        }

        private async void HandleSelectCharacterMessage(MsgSelectCharacter message)
        {
            var index = message.SelectedCharacterIndex;
            var userId = message.MsgChannel.UserId;

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded.IsCompleted)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (index < 0 || index >= MaxCharacterSlots)
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            if (!curPrefs.Characters.ContainsKey(index))
            {
                // Non-existent slot.
                return;
            }

            prefsData.Prefs = new PlayerPreferences(curPrefs.Characters, index, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                await _db.SaveSelectedCharacterIndexAsync(message.MsgChannel.UserId, message.SelectedCharacterIndex);
            }
        }

        private async void HandleUpdateCharacterMessage(MsgUpdateCharacter message)
        {
            var slot = message.Slot;
            var profile = message.Profile;
            var userId = message.MsgChannel.UserId;

            if (profile == null)
            {
                Logger.WarningS("prefs",
                    $"User {userId} sent a {nameof(MsgUpdateCharacter)} with a null profile in slot {slot}.");
                return;
            }

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded.IsCompleted)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (slot < 0 || slot >= MaxCharacterSlots)
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            profile.EnsureValid();

            var profiles = new Dictionary<int, ICharacterProfile>(curPrefs.Characters)
            {
                [slot] = profile
            };

            prefsData.Prefs = new PlayerPreferences(profiles, slot, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                await _db.SaveCharacterSlotAsync(message.MsgChannel.UserId, message.Profile, message.Slot);
            }
        }

        private async void HandleDeleteCharacterMessage(MsgDeleteCharacter message)
        {
            var slot = message.Slot;
            var userId = message.MsgChannel.UserId;

            if (!_cachedPlayerPrefs.TryGetValue(userId, out var prefsData) || !prefsData.PrefsLoaded.IsCompleted)
            {
                Logger.WarningS("prefs", $"User {userId} tried to modify preferences before they loaded.");
                return;
            }

            if (slot < 0 || slot >= MaxCharacterSlots)
            {
                return;
            }

            var curPrefs = prefsData.Prefs!;

            // If they try to delete the slot they have selected then we switch to another one.
            // Of course, that's only if they HAVE another slot.
            int? nextSlot = null;
            if (curPrefs.SelectedCharacterIndex == slot)
            {
                // That ! on the end is because Rider doesn't like .NET 5.
                var (ns, profile) = curPrefs.Characters.FirstOrDefault(p => p.Key != message.Slot)!;
                if (profile == null)
                {
                    // Only slot left, can't delete.
                    return;
                }

                nextSlot = ns;
            }

            var arr = new Dictionary<int, ICharacterProfile>(curPrefs.Characters);
            arr.Remove(slot);

            prefsData.Prefs = new PlayerPreferences(arr, nextSlot ?? curPrefs.SelectedCharacterIndex, curPrefs.AdminOOCColor);

            if (ShouldStorePrefs(message.MsgChannel.AuthType))
            {
                if (nextSlot != null)
                {
                    await _db.DeleteSlotAndSetSelectedIndex(userId, slot, nextSlot.Value);
                }
                else
                {
                    await _db.SaveCharacterSlotAsync(userId, null, slot);
                }
            }
        }

        public async void OnClientConnected(IPlayerSession session)
        {
            if (!ShouldStorePrefs(session.ConnectedClient.AuthType))
            {
                // Don't store data for guests.
                var prefsData = new PlayerPrefData
                {
                    PrefsLoaded = Task.CompletedTask,
                    Prefs = new PlayerPreferences(
                        new[] {new KeyValuePair<int, ICharacterProfile>(0, HumanoidCharacterProfile.Random())},
                        0, Color.Transparent)
                };

                _cachedPlayerPrefs[session.UserId] = prefsData;
            }
            else
            {
                var prefsData = new PlayerPrefData();
                var loadTask = LoadPrefs();
                prefsData.PrefsLoaded = loadTask;
                _cachedPlayerPrefs[session.UserId] = prefsData;

                await loadTask;

                async Task LoadPrefs()
                {
                    var prefs = await GetOrCreatePreferencesAsync(session.UserId);
                    prefsData.Prefs = prefs;

                    var msg = _netManager.CreateNetMessage<MsgPreferencesAndSettings>();
                    msg.Preferences = prefs;
                    msg.Settings = new GameSettings
                    {
                        MaxCharacterSlots = MaxCharacterSlots
                    };
                    _netManager.ServerSendMessage(msg, session.ConnectedClient);
                }
            }
        }


        public void OnClientDisconnected(IPlayerSession session)
        {
            _cachedPlayerPrefs.Remove(session.UserId);
        }

        public bool HavePreferencesLoaded(IPlayerSession session)
        {
            return _cachedPlayerPrefs.ContainsKey(session.UserId);
        }

        public Task WaitPreferencesLoaded(IPlayerSession session)
        {
            return _cachedPlayerPrefs[session.UserId].PrefsLoaded;
        }

        /// <summary>
        /// Retrieves preferences for the given username from storage.
        /// Creates and saves default preferences if they are not found, then returns them.
        /// </summary>
        public PlayerPreferences GetPreferences(NetUserId userId)
        {
            var prefs = _cachedPlayerPrefs[userId].Prefs;
            if (prefs == null)
            {
                throw new InvalidOperationException("Preferences for this player have not loaded yet.");
            }

            return prefs;
        }

        private async Task<PlayerPreferences> GetOrCreatePreferencesAsync(NetUserId userId)
        {
            var prefs = await _db.GetPlayerPreferencesAsync(userId);
            if (prefs is null)
            {
                return await _db.InitPrefsAsync(userId, HumanoidCharacterProfile.Random());
            }

            return SanitizePreferences(prefs);
        }

        private PlayerPreferences SanitizePreferences(PlayerPreferences prefs)
        {
            // Clean up preferences in case of changes to the game,
            // such as removed jobs still being selected.

            return new PlayerPreferences(prefs.Characters.Select(p =>
            {
                ICharacterProfile newProf;
                switch (p.Value)
                {
                    case HumanoidCharacterProfile hp:
                    {
                        newProf = hp
                            .WithJobPriorities(
                                hp.JobPriorities.Where(job =>
                                    _protos.HasIndex<JobPrototype>(job.Key)))
                            .WithAntagPreferences(
                                hp.AntagPreferences.Where(antag =>
                                    _protos.HasIndex<AntagPrototype>(antag)));
                        break;
                    }
                    default:
                        throw new NotSupportedException();
                }

                return new KeyValuePair<int, ICharacterProfile>(p.Key, newProf);
            }), prefs.SelectedCharacterIndex, prefs.AdminOOCColor);
        }

        public IEnumerable<KeyValuePair<NetUserId, ICharacterProfile>> GetSelectedProfilesForPlayers(
            List<NetUserId> usernames)
        {
            return usernames
                .Select(p => (_cachedPlayerPrefs[p].Prefs, p))
                .Where(p => p.Prefs != null)
                .Select(p =>
                {
                    var idx = p.Prefs!.SelectedCharacterIndex;
                    return new KeyValuePair<NetUserId, ICharacterProfile>(p.p, p.Prefs!.GetProfile(idx));
                });
        }

        internal static bool ShouldStorePrefs(LoginType loginType)
        {
            return loginType.HasStaticUserId();
        }

        private sealed class PlayerPrefData
        {
            public Task PrefsLoaded = default!;
            public PlayerPreferences? Prefs;
        }
    }
}
