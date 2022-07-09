using System.Diagnostics.CodeAnalysis;
using Content.Server.Access.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

/// <summary>
///     Station records.
///
///     A station record is tied to an ID card, or anything that holds
///     a station record's key. This key will determine access to a
///     station record set's record entries, and it is imperative not
///     to lose the item that holds the key under any circumstance.
///
///     Records are mostly a roleplaying tool, but can have some
///     functionality as well (i.e., security records indicating that
///     a specific person holding an ID card with a linked key is
///     currently under warrant, showing a crew manifest with user
///     settable, custom titles).
///
///     General records are tied into this system, as most crewmembers
///     should have a general record - and most systems should probably
///     depend on this general record being created. This is subject
///     to change.
/// </summary>
public sealed class StationRecordsSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly StationRecordKeyStorageSystem _keyStorageSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnStationInitialize(StationInitializedEvent args)
    {
        AddComp<StationRecordsComponent>(args.Station);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        CreateGeneralRecord(args.Station, args.Mob, args.Profile, args.JobId);
    }

    private void CreateGeneralRecord(EntityUid station, EntityUid player, HumanoidCharacterProfile profile,
        string? jobId, StationRecordsComponent? records = null)
    {
        if (!Resolve(station, ref records)
            || String.IsNullOrEmpty(jobId)
            || !_prototypeManager.HasIndex<JobPrototype>(jobId))
        {
            return;
        }

        if (!_inventorySystem.TryGetSlotEntity(player, "id", out var idUid))
        {
            return;
        }

        CreateGeneralRecord(station, idUid.Value, profile.Name, profile.Species, profile.Gender, jobId, profile, records);
    }


    /// <summary>
    ///     Create a general record to store in a station's record set.
    /// </summary>
    /// <remarks>
    ///     This is tied into the record system, as any crew member's
    ///     records should generally be dependent on some generic
    ///     record with the bare minimum of information involved.
    /// </remarks>
    /// <param name="station"></param>
    /// <param name="idUid"></param>
    /// <param name="name"></param>
    /// <param name="species"></param>
    /// <param name="gender"></param>
    /// <param name="jobId">
    ///     The job to initially tie this record to. This must be a valid job loaded in, otherwise
    ///     this call will silently fail. For example, just give somebody the 'passenger' job if
    ///     they want a new record.
    /// </param>
    /// <param name="profile">
    ///     Profile for the related player. This is so that other systems can get further information
    ///     about the player character.
    ///     Optional - other systems should anticipate this.
    /// </param>
    /// <param name="records"></param>
    public void CreateGeneralRecord(EntityUid station, EntityUid? idUid, string name, string species, Gender gender, string? jobId, HumanoidCharacterProfile? profile = null,
        StationRecordsComponent? records = null)
    {
        if (!Resolve(station, ref records)
            || string.IsNullOrEmpty(jobId)
            || !_prototypeManager.TryIndex(jobId, out JobPrototype? jobPrototype))
        {
            return;
        }

        var record = new GeneralStationRecord()
        {
            Name = name,
            JobTitle = jobPrototype.Name,
            JobIcon = jobPrototype.Icon,
            Species = species,
            Gender = gender,
            DisplayPriority = jobPrototype.Weight
        };

        record.Departments.AddRange(jobPrototype.Departments);

        var key = records.Records.AddRecord(station);
        records.Records.AddRecordEntry(key, record);
        // entry.Entries.Add(typeof(GeneralStationRecord), record);

        if (idUid != null)
        {
            var keyStorageEntity = idUid;
            if (TryComp(idUid, out PDAComponent? pdaComponent) && pdaComponent.ContainedID != null)
            {
                keyStorageEntity = pdaComponent.IdSlot.Item;
            }

            if (keyStorageEntity != null)
            {
                _keyStorageSystem.AssignKey(keyStorageEntity.Value, key);
            }
        }

        RaiseLocalEvent(new AfterGeneralRecordCreatedEvent(key, record, profile));
    }

    public bool RemoveRecord(EntityUid station, StationRecordKey key, StationRecordsComponent? records = null)
    {
        if (station != key.OriginStation || !Resolve(station, ref records))
        {
            return false;
        }

        RaiseLocalEvent(new RecordRemovedEvent(key));

        return records.Records.RemoveAllRecords(key);
    }

    /// <summary>
    ///     Try to get a record from this station's record entries,
    ///     from the provided station record key. Will always return
    ///     null if the key does not match the station.
    /// </summary>
    /// <param name="station"></param>
    /// <param name="key"></param>
    /// <param name="entry"></param>
    /// <param name="records"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public bool TryGetRecord<T>(EntityUid station, StationRecordKey key, [NotNullWhen(true)] out T? entry, StationRecordsComponent? records = null)
    {
        entry = default;

        if (key.OriginStation != station || !Resolve(station, ref records))
        {
            return false;
        }

        return records.Records.TryGetRecordEntry(key, out entry);
    }

    public IEnumerable<(StationRecordKey, T)?>? GetRecordsOfType<T>(EntityUid station, StationRecordsComponent? records = null)
    {
        if (!Resolve(station, ref records))
        {
            return null;
        }

        return records.Records.GetRecordsOfType<T>();
    }
}

/// <summary>
///     Event raised after the player's general profile is created.
///     Systems that modify records on a station would have more use
///     listening to this event, as it contains the character's record key.
///     Also stores the general record reference, to save some time.
/// </summary>
public sealed class AfterGeneralRecordCreatedEvent : EntityEventArgs
{
    public StationRecordKey Key { get; }
    public GeneralStationRecord Record { get; }
    /// <summary>
    /// Profile for the related player. This is so that other systems can get further information
    ///     about the player character.
    ///     Optional - other systems should anticipate this.
    /// </summary>
    public HumanoidCharacterProfile? Profile { get; }

    public AfterGeneralRecordCreatedEvent(StationRecordKey key, GeneralStationRecord record, HumanoidCharacterProfile? profile)
    {
        Key = key;
        Record = record;
        Profile = profile;
    }
}

/// <summary>
///     Event raised after a record is removed. Only the key is given
///     when the record is removed, so that any relevant systems/components
///     that store record keys can then remove the key from their internal
///     fields.
/// </summary>
public sealed class RecordRemovedEvent : EntityEventArgs
{
    public StationRecordKey Key { get; }

    public RecordRemovedEvent(StationRecordKey key)
    {
        Key = key;
    }
}

/// <summary>
///     Event raised after a record is modified. This is to
///     inform other systems that records stored in this key
///     may have changed.
/// </summary>
public sealed class RecordModifiedEvent : EntityEventArgs
{
    public StationRecordKey Key { get; }

    public RecordModifiedEvent(StationRecordKey key)
    {
        Key = key;
    }
}
