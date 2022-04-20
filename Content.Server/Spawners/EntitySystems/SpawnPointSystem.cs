﻿using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnPointSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnPlayerEvent>(OnSpawnPlayer);
    }

    private void OnSpawnPlayer(SpawnPlayerEvent args)
    {
        // TODO: Cache all this if it ends up important.
        var points = EntityQuery<SpawnPointComponent>().ToList();
        Logger.Debug($"B {args.Station}");
        _random.Shuffle(points);
        foreach (var spawnPoint in points)
        {
            var xform = Transform(spawnPoint.Owner);
            Logger.Debug($"Owner: {_stationSystem.GetOwningStation(spawnPoint.Owner, xform)}");
            if (args.Station != null && _stationSystem.GetOwningStation(spawnPoint.Owner, xform) != args.Station)
                continue;

            Logger.Debug($"A {spawnPoint.Job?.ID}");

            if (_gameTicker.RunLevel == GameRunLevel.InRound && spawnPoint.SpawnType == SpawnPointType.LateJoin)
            {
                args.SpawnResult = _stationSpawning.SpawnPlayerMob(xform.Coordinates, args.Job,
                    args.HumanoidCharacterProfile);
                return;
            }
            else if (_gameTicker.RunLevel != GameRunLevel.InRound && spawnPoint.SpawnType == SpawnPointType.Job && (args.Job == null || spawnPoint.Job?.ID == args.Job.Prototype.ID))
            {
                args.SpawnResult = _stationSpawning.SpawnPlayerMob(xform.Coordinates, args.Job,
                    args.HumanoidCharacterProfile);
                return;
            }
        }
    }
}
