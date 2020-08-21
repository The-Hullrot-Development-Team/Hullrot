﻿#nullable enable
using Content.Server.GameObjects.Components.Power.ApcNetComponents;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Research;
using Content.Shared.Interfaces.GameObjects.Components;
using Content.Shared.Research;
using Robust.Server.GameObjects.Components.UserInterface;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Server.Interfaces.Player;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Research
{
    [RegisterComponent]
    [ComponentReference(typeof(IActivate))]
    public class ResearchConsoleComponent : SharedResearchConsoleComponent, IActivate
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        private const string SoundCollectionName = "keyboard";

        private bool Powered => PowerReceiver == null || PowerReceiver.Powered;

        [ViewVariables]
        private BoundUserInterface? UserInterface =>
            Owner.TryGetComponent(out ServerUserInterfaceComponent? ui) &&
            ui.TryGetBoundUserInterface(ResearchConsoleUiKey.Key, out var boundUi)
                ? boundUi
                : null;

        private ResearchClientComponent? Client =>
            Owner.TryGetComponent(out ResearchClientComponent? research) ? research : null;

        private PowerReceiverComponent? PowerReceiver =>
            Owner.TryGetComponent(out PowerReceiverComponent? receiver) ? receiver : null;

        public override void Initialize()
        {
            base.Initialize();

            if (UserInterface != null)
            {
                UserInterface.OnReceiveMessage += UserInterfaceOnOnReceiveMessage;
            }

            Owner.EnsureComponent<ResearchClientComponent>();
        }

        private void UserInterfaceOnOnReceiveMessage(ServerBoundUserInterfaceMessage message)
        {
            if (!Owner.TryGetComponent(out TechnologyDatabaseComponent? database)) return;
            if (!Powered)
                return;

            switch (message.Message)
            {
                case ConsoleUnlockTechnologyMessage msg:
                    var protoMan = IoCManager.Resolve<IPrototypeManager>();
                    if (!protoMan.TryIndex(msg.Id, out TechnologyPrototype tech)) break;
                    if (Client?.Server == null) break;
                    if (!Client.Server.CanUnlockTechnology(tech)) break;
                    if (Client.Server.UnlockTechnology(tech))
                    {
                        database.SyncWithServer();
                        database.Dirty();
                        UpdateUserInterface();
                    }

                    break;

                case ConsoleServerSyncMessage _:
                    database.SyncWithServer();
                    UpdateUserInterface();
                    break;

                case ConsoleServerSelectionMessage _:
                    if (!Owner.TryGetComponent(out ResearchClientComponent? client)) break;
                    client.OpenUserInterface(message.Session);
                    break;
            }
        }

        /// <summary>
        ///     Method to update the user interface on the clients.
        /// </summary>
        public void UpdateUserInterface()
        {
            UserInterface?.SetState(GetNewUiState());
        }

        private ResearchConsoleBoundInterfaceState GetNewUiState()
        {
            if (Client?.Server == null)
                return new ResearchConsoleBoundInterfaceState(default, default);

            var points = Client.ConnectedToServer ? Client.Server.Point : 0;
            var pointsPerSecond = Client.ConnectedToServer ? Client.Server.PointsPerSecond : 0;

            return new ResearchConsoleBoundInterfaceState(points, pointsPerSecond);
        }

        /// <summary>
        ///     Open the user interface on a certain player session.
        /// </summary>
        /// <param name="session">Session where the UI will be shown</param>
        public void OpenUserInterface(IPlayerSession session)
        {
            UserInterface?.Open(session);
        }

        void IActivate.Activate(ActivateEventArgs eventArgs)
        {
            if (!eventArgs.User.TryGetComponent(out IActorComponent? actor))
                return;
            if (!Powered)
            {
                return;
            }

            OpenUserInterface(actor.playerSession);
            PlayKeyboardSound();
        }

        private void PlayKeyboardSound()
        {
            var soundCollection = _prototypeManager.Index<SoundCollectionPrototype>(SoundCollectionName);
            var file = _random.Pick(soundCollection.PickFiles);
            var audioSystem = EntitySystem.Get<AudioSystem>();
            audioSystem.PlayFromEntity(file,Owner,AudioParams.Default);
        }
    }
}
