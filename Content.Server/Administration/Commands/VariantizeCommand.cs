﻿using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class VariantizeCommand : IConsoleCommand
{

    public string Command => "variantize";

    public string Description => "Automatic variant randomization.";

    public string Help => "variantize <grid id>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var mapManager = IoCManager.Resolve<IMapManager>();
        var random = IoCManager.Resolve<IRobustRandom>();

        if (!int.TryParse(args[0], out var targetId))
        {
            shell.WriteLine(Loc.GetString("shell-argument-must-be-number"));
            return;
        }

        var gridId = new GridId(targetId);
        var grid = mapManager.GetGrid(gridId);
        foreach (var tile in grid.GetAllTiles())
        {
            var def = tile.GetContentTileDefinition();
            var newTile = new Tile(tile.Tile.TypeId, tile.Tile.Flags, (byte) random.Next(0, def.Variants));
            grid.SetTile(tile.GridIndices, newTile);
        }
    }
}
