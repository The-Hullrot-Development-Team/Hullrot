using System.Threading;
using System.Threading.Tasks;
using Content.Server.GameObjects.Components;
using Content.Server.GameObjects.EntitySystems.DoAfter;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.DoAfter
{
    [TestFixture]
    [TestOf(typeof(DoAfterComponent))]
    public class DoAfterServerTest : ContentIntegrationTest
    {
        private const string Prototypes = @"
- type: entity
  name: Dummy
  id: Dummy
  components:
  - type: DoAfter
";

        [Test]
        public void TestFinished()
        {
            Task<DoAfterStatus> task = null;
            var options = new ServerIntegrationOptions{ExtraPrototypes = Prototypes};
            var server = StartServerDummyTicker(options);

            // That it finishes successfully
            server.Post(() =>
            {
                var tickTime = 1.0f / IoCManager.Resolve<IGameTiming>().TickRate;
                var mapManager = IoCManager.Resolve<IMapManager>();
                mapManager.CreateNewMapEntity(MapId.Nullspace);
                var entityManager = IoCManager.Resolve<IEntityManager>();
                var mob = entityManager.SpawnEntity("Dummy", MapCoordinates.Nullspace);
                var cancelToken = new CancellationTokenSource();
                var args = new DoAfterEventArgs(mob, tickTime / 2, cancelToken.Token);
                task = EntitySystem.Get<DoAfterSystem>().DoAfter(args);
            });

            server.WaitRunTicks(1);
            Assert.That(task.Result == DoAfterStatus.Finished);
        }

        [Test]
        public void TestCancelled()
        {
            Task<DoAfterStatus> task = null;
            var options = new ServerIntegrationOptions{ExtraPrototypes = Prototypes};
            var server = StartServerDummyTicker(options);

            server.Post(() =>
            {
                var tickTime = 1.0f / IoCManager.Resolve<IGameTiming>().TickRate;
                var mapManager = IoCManager.Resolve<IMapManager>();
                mapManager.CreateNewMapEntity(MapId.Nullspace);
                var entityManager = IoCManager.Resolve<IEntityManager>();
                var mob = entityManager.SpawnEntity("Dummy", MapCoordinates.Nullspace);
                var cancelToken = new CancellationTokenSource();
                var args = new DoAfterEventArgs(mob, tickTime * 2, cancelToken.Token);
                task = EntitySystem.Get<DoAfterSystem>().DoAfter(args);
                cancelToken.Cancel();
            });

            server.WaitRunTicks(3);
            Assert.That(task.Result == DoAfterStatus.Cancelled, $"Result was {task.Result}");
        }
    }
}
