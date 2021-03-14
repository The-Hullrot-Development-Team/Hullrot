using Content.Client;
using Content.Client.Interfaces;
using Content.Client.State;
using Content.Server.GameTicking;
using Content.Server.Interfaces;
using Content.Server.Interfaces.GameTicking;
using Content.Server.Preferences;
using Content.Shared;
using Content.Shared.Preferences;
using NUnit.Framework;
using Robust.Client.State;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Lobby
{
    [TestFixture]
    [TestOf(typeof(ClientPreferencesManager))]
    [TestOf(typeof(ServerPreferencesManager))]
    public class CharacterCreationTest : ContentIntegrationTest
    {
        [Test]
        public void CreateDeleteCreateTest()
        {
            var (client, server) = StartConnectedServerClientPair();

            var clientNetManager = client.ResolveDependency<IClientNetManager>();
            var clientStateManager = client.ResolveDependency<IStateManager>();
            var clientPrefManager = client.ResolveDependency<IClientPreferencesManager>();

            var serverConfig = server.ResolveDependency<IConfigurationManager>();
            var serverTicker = server.ResolveDependency<IGameTicker>();
            var serverPrefManager = server.ResolveDependency<IServerPreferencesManager>();

            server.WaitIdleAsync();
            client.WaitIdleAsync();

            server.WaitAssertion(() =>
            {
                serverConfig.SetCVar(CCVars.GameLobbyEnabled, true);
                serverTicker.RestartRound();
            });

            Assert.That(serverTicker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

            WaitUntil(client, () => clientStateManager.CurrentState is LobbyState, maxTicks: 60);

            Assert.NotNull(clientNetManager.ServerChannel);

            var clientNetId = clientNetManager.ServerChannel.UserId;
            HumanoidCharacterProfile profile = null;

            client.WaitAssertion(() =>
            {
                clientPrefManager.SelectCharacter(0);

                var clientCharacters = clientPrefManager.Preferences.Characters;
                Assert.That(clientCharacters.Count, Is.EqualTo(1));

                Assert.That(clientStateManager.CurrentState, Is.TypeOf<LobbyState>());

                profile = HumanoidCharacterProfile.Random();
                clientPrefManager.CreateCharacter(profile);

                clientCharacters = clientPrefManager.Preferences.Characters;

                Assert.That(clientCharacters.Count, Is.EqualTo(2));
                Assert.That(clientCharacters[1].MemberwiseEquals(profile));
            });

            WaitUntil(server, () => serverPrefManager.GetPreferences(clientNetId).Characters.Count == 2, maxTicks: 60);

            server.WaitAssertion(() =>
            {
                var serverCharacters = serverPrefManager.GetPreferences(clientNetId).Characters;

                Assert.That(serverCharacters.Count, Is.EqualTo(2));
                Assert.That(serverCharacters[1].MemberwiseEquals(profile));
            });

            client.WaitAssertion(() =>
            {
                clientPrefManager.DeleteCharacter(1);

                var clientCharacters = clientPrefManager.Preferences.Characters.Count;
                Assert.That(clientCharacters, Is.EqualTo(1));
            });

            WaitUntil(server, () => serverPrefManager.GetPreferences(clientNetId).Characters.Count == 1, maxTicks: 60);

            server.WaitAssertion(() =>
            {
                var serverCharacters = serverPrefManager.GetPreferences(clientNetId).Characters.Count;
                Assert.That(serverCharacters, Is.EqualTo(1));
            });

            client.WaitIdleAsync();

            client.WaitAssertion(() =>
            {
                profile = HumanoidCharacterProfile.Random();

                clientPrefManager.CreateCharacter(profile);

                var clientCharacters = clientPrefManager.Preferences.Characters;

                Assert.That(clientCharacters.Count, Is.EqualTo(2));
                Assert.That(clientCharacters[1].MemberwiseEquals(profile));
            });

            WaitUntil(server, () => serverPrefManager.GetPreferences(clientNetId).Characters.Count == 2, maxTicks: 60);

            server.WaitAssertion(() =>
            {
                var serverCharacters = serverPrefManager.GetPreferences(clientNetId).Characters;

                Assert.That(serverCharacters.Count, Is.EqualTo(2));
                Assert.That(serverCharacters[1].MemberwiseEquals(profile));
            });
        }
    }
}
