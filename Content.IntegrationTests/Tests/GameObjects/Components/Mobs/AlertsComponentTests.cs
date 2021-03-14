using System.Linq;
using Content.Client.GameObjects.Components.Mobs;
using Content.Client.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Server.GameObjects.Components.Mobs;
using Content.Shared.Alert;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Server.Player;

namespace Content.IntegrationTests.Tests.GameObjects.Components.Mobs
{
    [TestFixture]
    [TestOf(typeof(ClientAlertsComponent))]
    [TestOf(typeof(ServerAlertsComponent))]
    public class AlertsComponentTests : ContentIntegrationTest
    {
        [Test]
        public void AlertsTest()
        {
            var (client, server) = StartConnectedServerClientPair();

            server.WaitIdleAsync();
            client.WaitIdleAsync();

            var serverPlayerManager = server.ResolveDependency<IPlayerManager>();

            server.WaitAssertion(() =>
            {
                var player = serverPlayerManager.GetAllPlayers().Single();
                var playerEnt = player.AttachedEntity;
                Assert.NotNull(playerEnt);
                var alertsComponent = playerEnt.GetComponent<ServerAlertsComponent>();
                Assert.NotNull(alertsComponent);

                // show 2 alerts
                alertsComponent.ShowAlert(AlertType.Debug1);
                alertsComponent.ShowAlert(AlertType.Debug2);
            });

            server.WaitRunTicks(5);
            client.WaitRunTicks(5);

            var clientPlayerMgr = client.ResolveDependency<Robust.Client.Player.IPlayerManager>();
            var clientUIMgr = client.ResolveDependency<IUserInterfaceManager>();
            client.WaitAssertion(() =>
            {

                var local = clientPlayerMgr.LocalPlayer;
                Assert.NotNull(local);
                var controlled = local.ControlledEntity;
                Assert.NotNull(controlled);
                var alertsComponent = controlled.GetComponent<ClientAlertsComponent>();
                Assert.NotNull(alertsComponent);

                // find the alertsui
                var alertsUI =
                    clientUIMgr.StateRoot.Children.FirstOrDefault(c => c is AlertsUI) as AlertsUI;
                Assert.NotNull(alertsUI);

                // we should be seeing 3 alerts - our health, and the 2 debug alerts, in a specific order.
                Assert.That(alertsUI.Grid.ChildCount, Is.GreaterThanOrEqualTo(3));
                var alertControls = alertsUI.Grid.Children.Select(c => (AlertControl) c);
                var alertIDs = alertControls.Select(ac => ac.Alert.AlertType).ToArray();
                var expectedIDs = new [] {AlertType.HumanHealth, AlertType.Debug1, AlertType.Debug2};
                Assert.That(alertIDs, Is.SupersetOf(expectedIDs));
            });

            server.WaitAssertion(() =>
            {
                var player = serverPlayerManager.GetAllPlayers().Single();
                var playerEnt = player.AttachedEntity;
                Assert.NotNull(playerEnt);
                var alertsComponent = playerEnt.GetComponent<ServerAlertsComponent>();
                Assert.NotNull(alertsComponent);

                alertsComponent.ClearAlert(AlertType.Debug1);
            });
            server.WaitRunTicks(5);
            client.WaitRunTicks(5);

            client.WaitAssertion(() =>
            {

                var local = clientPlayerMgr.LocalPlayer;
                Assert.NotNull(local);
                var controlled = local.ControlledEntity;
                Assert.NotNull(controlled);
                var alertsComponent = controlled.GetComponent<ClientAlertsComponent>();
                Assert.NotNull(alertsComponent);

                // find the alertsui
                var alertsUI =
                    clientUIMgr.StateRoot.Children.FirstOrDefault(c => c is AlertsUI) as AlertsUI;
                Assert.NotNull(alertsUI);

                // we should be seeing 2 alerts now because one was cleared
                Assert.That(alertsUI.Grid.ChildCount, Is.GreaterThanOrEqualTo(2));
                var alertControls = alertsUI.Grid.Children.Select(c => (AlertControl) c);
                var alertIDs = alertControls.Select(ac => ac.Alert.AlertType).ToArray();
                var expectedIDs = new [] {AlertType.HumanHealth, AlertType.Debug2};
                Assert.That(alertIDs, Is.SupersetOf(expectedIDs));
            });
        }
    }
}
