using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using static Content.Server.Station.StationSystem;

namespace Content.Server.GameTicking
{
    // This code is responsible for the assigning & picking of jobs.
    public partial class GameTicker
    {
        [ViewVariables]
        private readonly List<ManifestEntry> _manifest = new();

        [ViewVariables]
        private readonly Dictionary<string, int> _spawnedPositions = new();

        private Dictionary<IPlayerSession, (string, StationId)> AssignJobs(List<IPlayerSession> available,
            Dictionary<NetUserId, HumanoidCharacterProfile> profiles)
        {
            var assigned = new Dictionary<IPlayerSession, (string, StationId)>();
            // Current strategy is to fill each station one by one.
            foreach (var (id, station) in _stationSystem.StationInfo)
            {
                // Get the ROUND-START job list.
                var availablePositions = station.MapPrototype.AvailableJobs.ToDictionary(x => x.Key, x => x.Value[0]);
                for (var i = JobPriority.High; i > JobPriority.Never; i--)
                {
                    void ProcessJobs(bool heads)
                    {
                        var candidates = available.Select(player =>
                            {
                                var profile = profiles[player.UserId];

                                var availableJobs = profile.JobPriorities
                                    .Where(j =>
                                    {
                                        var (jobId, priority) = j;
                                        if (!_prototypeManager.TryIndex(jobId, out JobPrototype? job))
                                        {
                                            // Job doesn't exist, probably old data?
                                            return false;
                                        }

                                        if (job.IsHead != heads)
                                        {
                                            return false;
                                        }

                                        return priority == i;
                                    })
                                    .Select(j => j.Key)
                                    .ToList();

                                return (player, availableJobs);
                            })
                            .Where(p => p.availableJobs.Count != 0)
                            .ToList();

                        foreach (var (candidate, jobs) in candidates)
                        {
                            while (jobs.Count != 0)
                            {
                                var picked = _robustRandom.Pick(jobs);

                                var openPositions = availablePositions.GetValueOrDefault(picked, 0);
                                if (openPositions == 0)
                                {
                                    jobs.Remove(picked);
                                    continue;
                                }

                                availablePositions[picked] -= 1;
                                assigned.Add(candidate, (picked, id));
                                break;
                            }
                        }

                        available.RemoveAll(a => assigned.ContainsKey(a));
                    }
                    ProcessJobs(true);
                    ProcessJobs(false);
                }
            }

            return assigned;
        }

        private string PickBestAvailableJob(HumanoidCharacterProfile profile, StationId station)
        {
            var available = _stationSystem.StationInfo[station].JobList;

            bool TryPick(JobPriority priority, [NotNullWhen(true)] out string? jobId)
            {
                var filtered = profile.JobPriorities
                    .Where(p => p.Value == priority)
                    .Select(p => p.Key)
                    .ToList();

                while (filtered.Count != 0)
                {
                    jobId = _robustRandom.Pick(filtered);
                    if (available.GetValueOrDefault(jobId, 0) > 0)
                    {
                        return true;
                    }

                    filtered.Remove(jobId);
                }

                jobId = default;
                return false;
            }

            if (TryPick(JobPriority.High, out var picked))
            {
                return picked;
            }

            if (TryPick(JobPriority.Medium, out picked))
            {
                return picked;
            }

            if (TryPick(JobPriority.Low, out picked))
            {
                return picked;
            }

            var overflows = _stationSystem.StationInfo[station].MapPrototype.OverflowJobs.Clone();
            _robustRandom.Shuffle(overflows);
            return overflows[0];
        }

        [Conditional("DEBUG")]
        private void InitializeJobController()
        {
            // Verify that the overflow role exists and has the correct name.
            var role = _prototypeManager.Index<JobPrototype>(FallbackOverflowJob);
            DebugTools.Assert(role.Name == Loc.GetString(FallbackOverflowJobName),
                "Overflow role does not have the correct name!");
        }

        private void AddSpawnedPosition(string jobId)
        {
            _spawnedPositions[jobId] = _spawnedPositions.GetValueOrDefault(jobId, 0) + 1;
        }

        private TickerJobsAvailableEvent GetJobsAvailable()
        {
            // If late join is disallowed, return no available jobs.
            if (DisallowLateJoin)
                return new TickerJobsAvailableEvent(new Dictionary<uint, string>(), new Dictionary<uint, Dictionary<string, int>>());

            var jobs = new Dictionary<uint, Dictionary<string, int>>();
            var stationNames = new Dictionary<uint, string>();

            foreach (var (id, station) in _stationSystem.StationInfo)
            {
                var list = station.JobList.ToDictionary(x => x.Key, x => x.Value);
                jobs.Add(id.Id, list);
                stationNames.Add(id.Id, station.Name);
            }
            return new TickerJobsAvailableEvent(stationNames, jobs);
        }

        public void UpdateJobsAvailable()
        {
            RaiseNetworkEvent(GetJobsAvailable(), Filter.Empty().AddPlayers(_playersInLobby.Keys));
        }
    }
}
