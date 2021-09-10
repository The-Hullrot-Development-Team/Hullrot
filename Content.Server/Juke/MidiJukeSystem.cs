using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Content.Server.UserInterface;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Juke;
using JetBrains.Annotations;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Robust.Server.Audio.Midi;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Juke
{
    [UsedImplicitly]
    public class MidiJukeSystem : SharedMidiJukeSystem
    {
        [Dependency] private readonly IRobustRandom _robustRandom = default!;

        private readonly SortedList<string, string> _midiFiles = new();
        private const string MidiPath = "data/midis/"; //trailing slash es muchos importante
        private double _updateAccumulator = 0; //there must be a better way

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MidiJukeComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<MidiJukeComponent, InteractHandEvent>(OnInteractHand);

            //TODO: "what you want is [IWritableDirProvider.cs], use UserData in [IResourceManager.cs]" (use this instead of System.IO
            var loaded = 0;
            var failed = 0;
            foreach (var file in System.IO.Directory.GetFiles(MidiPath, "*.mid"))
            {
                var fileName = System.IO.Path.GetFileName(file);
                try
                {
                    var midiFile = MidiFile.Read(file, VirtualMidiPlayer.DefaultReadingSettings);
                    var timedEvents = midiFile.GetTrackChunks().GetTimedEvents();
                    string title = string.Empty;
                    if (timedEvents.First(x => x.Event is SequenceTrackNameEvent).Event is SequenceTrackNameEvent trackName)
                    {
                        title = trackName.Text;
                    }
                    _midiFiles.Add(fileName, title == string.Empty ? fileName : title);
                    loaded++;
                }
                catch (Exception e)
                {
                    Logger.Error($"Failed to parse MIDI file {fileName}: {e.GetType()}: {e.Message}");
                    failed++;
                }
            }
            Logger.InfoS("MidiJukeSystem", $"Loaded {loaded} MIDI files in x seconds. {failed} files failed to load.");
        }

        private void OnStartup(EntityUid uid, MidiJukeComponent midiJuke, ComponentStartup args)
        {
            //TODO: this is apparently bad and will need to be rewritten once BoundUserInterface becomes ECS friendlier
            var ui = midiJuke.Owner.GetUIOrNull(MidiJukeUiKey.Key);
            if (ui != null)
            {
                ui.OnReceiveMessage += msg => OnMidiJukeUiMessage(uid, midiJuke, msg);
                ui.SetState(GetState(midiJuke));
            }
            //OpenMidiFile(component, "testmidi.mid"); //TODO: remove this placeholder
        }

        private void DirtyUI(EntityUid uid)
        {
            if (!ComponentManager.TryGetComponent(uid, out MidiJukeComponent midiJuke)
                || !ComponentManager.TryGetComponent(uid, out ServerUserInterfaceComponent userInterfaceComponent)
                || !userInterfaceComponent.TryGetBoundUserInterface(MidiJukeUiKey.Key, out var ui))
                return;

            ui.SetState(GetState(midiJuke));
            UpdateTimestamp(uid, midiJuke.PlaybackStatus == MidiJukePlaybackStatus.Stop);
        }

        private MidiJukeBoundUserInterfaceState GetState(MidiJukeComponent component)
        {
            var currentSong = System.IO.Path.GetFileName(component.MidiFileName);
            var currentSongTitle = _midiFiles.GetValueOrDefault(currentSong, "");
            return new MidiJukeBoundUserInterfaceState(component.PlaybackStatus, component.Loop,
                currentSong, currentSongTitle);
        }

        private void UpdateSongList(EntityUid uid)
        {
            if (!ComponentManager.TryGetComponent(uid, out MidiJukeComponent midiJuke)
                || !ComponentManager.TryGetComponent(uid, out ServerUserInterfaceComponent userInterfaceComponent)
                || !userInterfaceComponent.TryGetBoundUserInterface(MidiJukeUiKey.Key, out var ui))
                return;
            ui.SendMessage(new MidiJukeSongListMessage(_midiFiles.Keys.ToList()));
        }

        private void UpdateTimestamp(EntityUid uid, bool blank = false)
        {
            if (!ComponentManager.TryGetComponent(uid, out MidiJukeComponent midiJuke)
                || !ComponentManager.TryGetComponent(uid, out ServerUserInterfaceComponent userInterfaceComponent)
                || !userInterfaceComponent.TryGetBoundUserInterface(MidiJukeUiKey.Key, out var ui))
                return;

            int? elapsed, duration;
            if (blank)
            {
                elapsed = duration = null;
            }
            else
            {
                elapsed = midiJuke.MidiPlayer?.CurrentTime;
                duration = midiJuke.MidiPlayer?.Duration;
            }
            ui.SendMessage(new MidiJukeTimestampMessage(elapsed, duration));
        }

        private void OnMidiJukeUiMessage(EntityUid uid, MidiJukeComponent component, ServerBoundUserInterfaceMessage msg)
        {
            var entity = msg.Session.AttachedEntity;
            if (entity == null
                || !Get<ActionBlockerSystem>().CanInteract(entity)
                || !Get<ActionBlockerSystem>().CanUse(entity))
                return;

            switch (msg.Message)
            {
                case MidiJukePlayMessage:
                    Play(component);
                    break;
                case MidiJukePauseMessage:
                    Pause(component);
                    break;
                case MidiJukeStopMessage:
                    Stop(component);
                    break;
                case MidiJukeSkipMessage:
                    ShuffleSong(component);
                    break;
                case MidiJukeLoopMessage loopMsg:
                    SetLoop(component, loopMsg.Loop);
                    break;
                case MidiJukeSongSelectMessage songMsg:
                    Stop(component);
                    OpenMidiFile(component, MidiPath + songMsg.Song);
                    Play(component);
                    break;
                case MidiJukeSongListRequestMessage:
                    UpdateSongList(uid);
                    break;
            }

            component.Dirty();
            DirtyUI(component.Owner.Uid);
        }

        private void OnInteractHand(EntityUid uid, MidiJukeComponent component, InteractHandEvent args)
        {
            if (!args.User.TryGetComponent(out ActorComponent? actor)) return;
            component.Owner.GetUIOrNull(MidiJukeUiKey.Key)?.Open(actor.PlayerSession);
            args.Handled = true;
        }

        public override void Update(float frameTime)
        {
            _updateAccumulator += frameTime;
            var updateTimestamp = false;
            if (_updateAccumulator >= 1)
            {
                updateTimestamp = true;
                _updateAccumulator = 0;
            }
            foreach (var component in ComponentManager.EntityQuery<MidiJukeComponent>(true))
            {
                if (!component.Playing || component.MidiPlayer == null) continue;
                var midiEvents = component.MidiPlayer?.TickClockAndPopEventBuffer();
                if (midiEvents == null) continue;
                var uid = component.Owner.Uid;
                RaiseNetworkEvent(new MidiJukeMidiEventsEvent(uid, midiEvents.ToArray()), Filter.Pvs(component.Owner));
                if (updateTimestamp)
                    UpdateTimestamp(uid);
            }
        }

        /// <summary>
        /// Loads a MIDI file into the component's MIDI player.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="filename"></param>
        private void OpenMidiFile(MidiJukeComponent component, string filename)
        {
            component.MidiPlayer?.Dispose();
            component.MidiPlayer = VirtualMidiPlayer.FromFile(filename);
            if (component.MidiPlayer != null)
            {
                component.MidiFileName = filename;
                component.MidiPlayer.Finished += (sender, eventArgs) => OnPlaybackFinished(component); //LOW KEY WORRIED ABOUT THE MEMORY SAFETY OF THIS
                component.MidiPlayer.Loop = component.Loop;
                component.MidiPlayer.EventPlayed += (sender, eventArgs) =>
                {
                    if (eventArgs.Event is ProgramChangeEvent programChangeEvent)
                    {
                        component.ChannelPrograms[programChangeEvent.Channel] = programChangeEvent.ProgramNumber;
                        component.Dirty();
                    }
                };
            }
        }

        private void OnPlaybackFinished(MidiJukeComponent component)
        {
            // component.MidiPlayer?.MoveToStart();
            // component.PlaybackStatus = MidiJukePlaybackStatus.Stop;
            // RaiseNetworkEvent(new MidiJukePlaybackFinishedEvent(component.Owner.Uid), Filter.Pvs(component.Owner));
            Logger.Debug("Playback finished, shuffling song.");
            ShuffleSong(component);
            DirtyUI(component.Owner.Uid);
        }

        /// <summary>
        /// Starts playback on the juke.
        /// </summary>
        /// <param name="component"></param>
        private void Play(MidiJukeComponent component)
        {
            if (!component.Playing && component.MidiPlayer != null)
            {
                component.MidiPlayer.Start();
                component.PlaybackStatus = MidiJukePlaybackStatus.Play;
                RaiseNetworkEvent(new MidiJukePlayEvent(component.Owner.Uid, component.MidiFileName), Filter.Pvs(component.Owner));
            }
        }

        /// <summary>
        /// Pauses playback, maintaining position in the file.
        /// </summary>
        /// <param name="component"></param>
        private void Pause(MidiJukeComponent component)
        {
            if (component.Playing && component.MidiPlayer != null)
            {
                component.MidiPlayer.Stop();
                component.PlaybackStatus = MidiJukePlaybackStatus.Pause;
                component.MidiPlayer.FlushEventBuffer();
                RaiseNetworkEvent(new MidiJukePauseEvent(component.Owner.Uid), Filter.Pvs(component.Owner));
            }
        }

        /// <summary>
        /// Stops playback, resetting position in the file to the beginning.
        /// </summary>
        /// <param name="component"></param>
        private void Stop(MidiJukeComponent component)
        {
            if (component.MidiPlayer != null)
            {
                component.MidiPlayer.Stop();
                component.PlaybackStatus = MidiJukePlaybackStatus.Stop;
                component.MidiPlayer.FlushEventBuffer();
                RaiseNetworkEvent(new MidiJukeStopEvent(component.Owner.Uid), Filter.Pvs(component.Owner));
                component.MidiPlayer.MoveToStart();
            }
        }

        private void ShuffleSong(MidiJukeComponent component)
        {
            if (component.MidiPlayer != null)
            {
                component.MidiPlayer.Dispose();
                component.MidiPlayer = null;
            }

            component.PlaybackStatus = MidiJukePlaybackStatus.Stop;
            var nextSong = _robustRandom.Pick(_midiFiles.Keys.ToList());
            OpenMidiFile(component, MidiPath + nextSong);
            RaiseNetworkEvent(new MidiJukePlaybackFinishedEvent(component.Owner.Uid), Filter.Pvs(component.Owner));
            if (component.MidiPlayer == null)
            {
                return;
            }
            Play(component);
        }

        private void SetLoop(MidiJukeComponent component, bool loop)
        {
            component.Loop = loop;
            if (component.MidiPlayer != null)
            {
                component.MidiPlayer.Loop = loop;
            }
        }
    }
}
