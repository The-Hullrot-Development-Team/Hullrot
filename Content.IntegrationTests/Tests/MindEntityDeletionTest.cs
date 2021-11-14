﻿using System.Linq;
using System.Threading.Tasks;
using Content.Server.Mind;
using Content.Server.Mind.Systems;
using Content.Server.Players;
using Content.Shared.Coordinates;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests
{
    // Tests various scenarios of deleting the entity that a player's mind is connected to.
    [TestFixture]
    public class MindEntityDeletionTest : ContentIntegrationTest
    {
        [Test]
        public async Task TestDeleteVisiting()
        {
            var (_, server) = await StartConnectedServerDummyTickerClientPair();

            IEntity playerEnt = null;
            IEntity visitEnt = null;
            Mind mind = null;
            server.Assert(() =>
            {
                var player = IoCManager.Resolve<IPlayerManager>().GetAllPlayers().Single();

                var mapMan = IoCManager.Resolve<IMapManager>();
                var entMgr = IoCManager.Resolve<IServerEntityManager>();
                var mindSys = EntitySystem.Get<MindSystem>();

                mapMan.CreateNewMapEntity(MapId.Nullspace);

                playerEnt = entMgr.SpawnEntity(null, MapCoordinates.Nullspace);
                visitEnt = entMgr.SpawnEntity(null, MapCoordinates.Nullspace);

                mind = new Mind(player.UserId);
                player.ContentData().Mind = mind;

                mindSys.TransferTo(mind, playerEnt.Uid);
                mindSys.Visit(mind, visitEnt);

                Assert.That(player.AttachedEntity, Is.EqualTo(visitEnt));
                Assert.That(mind.VisitingEntity, Is.EqualTo(visitEnt));
            });

            server.RunTicks(1);

            server.Assert(() =>
            {
                visitEnt.Delete();

                Assert.That(mind.VisitingEntity, Is.Null);

                // This used to throw so make sure it doesn't.
                playerEnt.Delete();
            });

            await server.WaitIdleAsync();
        }

        [Test]
        public async Task TestGhostOnDelete()
        {
            // Has to be a non-dummy ticker so we have a proper map.
            var (_, server) = await StartConnectedServerClientPair();

            IEntity playerEnt = null;
            Mind mind = null;
            server.Assert(() =>
            {
                var player = IoCManager.Resolve<IPlayerManager>().GetAllPlayers().Single();

                var mapMan = IoCManager.Resolve<IMapManager>();
                var entMgr = IoCManager.Resolve<IServerEntityManager>();
                var mindSys = EntitySystem.Get<MindSystem>();

                mapMan.CreateNewMapEntity(MapId.Nullspace);

                playerEnt = entMgr.SpawnEntity(null, MapCoordinates.Nullspace);

                mind = new Mind(player.UserId);
                player.ContentData().Mind = mind;

                mindSys.TransferTo(mind, playerEnt.Uid);

                var ent = entMgr.GetEntity(mind.CurrentEntity!.Value);
                Assert.That(ent, Is.EqualTo(playerEnt));
            });

            server.RunTicks(1);

            server.Post(() =>
            {
                playerEnt.Delete();
            });

            server.RunTicks(1);

            server.Assert(() =>
            {
                var entMgr = IoCManager.Resolve<IServerEntityManager>();
                var ent = entMgr.GetEntity(mind.CurrentEntity!.Value);
                Assert.That(ent.IsValid(), Is.True);
            });

            await server.WaitIdleAsync();
        }

        [Test]
        public async Task TestGhostOnDeleteMap()
        {
            // Has to be a non-dummy ticker so we have a proper map.
            var (_, server) = await StartConnectedServerClientPair();

            IEntity playerEnt = null;
            Mind mind = null;
            MapId map = default;
            server.Assert(() =>
            {
                var player = IoCManager.Resolve<IPlayerManager>().GetAllPlayers().Single();
                var mindSys = EntitySystem.Get<MindSystem>();
                var mapMan = IoCManager.Resolve<IMapManager>();

                map = mapMan.CreateMap();
                var grid = mapMan.CreateGrid(map);

                var entMgr = IoCManager.Resolve<IServerEntityManager>();

                mapMan.CreateNewMapEntity(MapId.Nullspace);

                playerEnt = entMgr.SpawnEntity(null, grid.ToCoordinates());

                mind = new Mind(player.UserId);
                player.ContentData().Mind = mind;

                mindSys.TransferTo(mind, playerEnt.Uid);

                var ent = entMgr.GetEntity(mind.CurrentEntity!.Value);
                Assert.That(ent, Is.EqualTo(playerEnt));
            });

            server.RunTicks(1);

            server.Post(() =>
            {
                var mapMan = IoCManager.Resolve<IMapManager>();

                mapMan.DeleteMap(map);
            });

            server.RunTicks(1);

            server.Assert(() =>
            {
                var entMgr = IoCManager.Resolve<IServerEntityManager>();
                var ent = entMgr.GetEntity(mind.CurrentEntity!.Value);
                Assert.That(ent.IsValid(), Is.True);
                Assert.That(ent, Is.Not.EqualTo(playerEnt));
            });

            await server.WaitIdleAsync();
        }
    }
}
