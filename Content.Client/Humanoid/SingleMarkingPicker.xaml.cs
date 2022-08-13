using System.Linq;
using Content.Shared.Markings;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Humanoid;

[GenerateTypedNameReferences]
public sealed partial class SingleMarkingPicker : BoxContainer
{
    [Dependency] private MarkingManager _markingManager = default!;

    /// <summary>
    ///     What happens if a marking is selected.
    ///     It will send the 'slot' (marking index)
    ///     and the selected marking's ID.
    /// </summary>
    public Action<(int, string)>? OnMarkingSelect;
    /// <summary>
    ///     What happens if a slot is removed.
    ///     This will send the 'slot' (marking index).
    /// </summary>
    public Action<int>? OnSlotRemove;

    /// <summary>
    ///     What happens when a slot is added.
    /// </summary>
    public Action? OnSlotAdd;

    /// <summary>
    ///     What happens if a marking's color is changed.
    ///     Sends a 'slot' number, and the marking in question.
    /// </summary>
    public Action<(int, Marking)>? OnColorChanged;

    // current selected slot
    private int _slot = -1;
    private int Slot
    {
        get
        {
            if (_markings == null || _markings.Count == 0)
            {
                _slot = -1;
            }

            return _slot;
        }
        set
        {
            if (_markings == null || _markings.Count == 0)
            {
                _slot = -1;
                return;
            }

            _slot = value;
            _ignoreItemSelected = true;

            foreach (var item in MarkingList)
            {
                item.Selected = (string) item.Metadata! == _markings[_slot].MarkingId;
            }

            _ignoreItemSelected = false;
            PopulateColors();
        }
    }

    // amount of slots to show
    private uint _pointsUsed;
    private uint _totalPoints;

    private bool _ignoreItemSelected;

    private MarkingCategories _category;
    public MarkingCategories Category
    {
        get => _category;
        set
        {
            if (!string.IsNullOrEmpty(_species))
            {
                PopulateList();
            }
        }
    }
    private IReadOnlyDictionary<string, MarkingPrototype>? _markingPrototypeCache;

    private string? _species;
    private List<Marking>? _markings;

    private int PointsLeft => _markings != null ? (int) _totalPoints - _markings.Count : 0;
    private int PointsUsed => _markings != null ? _markings.Count : 0;

    public SingleMarkingPicker()
    {
        IoCManager.InjectDependencies(this);

        MarkingList.OnItemSelected += SelectMarking;
        AddButton.OnPressed += _ =>
        {
            OnSlotAdd!();
        };

        SlotSelector.OnItemSelected += args =>
        {
            Slot = args.Button.SelectedId;
        };

        RemoveButton.OnPressed += _ =>
        {
            OnSlotRemove!(_slot);
        };
    }

    public void UpdateData(List<Marking> markings, string species, uint totalPoints)
    {
        _markings = markings;
        _species = species;
        _totalPoints = totalPoints;

        _markingPrototypeCache = _markingManager.MarkingsByCategoryAndSpecies(Category, _species);

        Visible = _markingPrototypeCache.Count != 0;
        if (_markingPrototypeCache.Count == 0)
        {
            return;
        }

        PopulateList();
        PopulateColors();
        PopulateSlotSelector();
    }

    public void PopulateList()
    {
        if (string.IsNullOrEmpty(_species))
        {
            throw new ArgumentException("Tried to populate marking list without a set species!");
        }

        MarkingSelectorContainer.Visible = _markings != null && _markings.Count != 0;
        if (_markings == null || _markings.Count == 0)
        {
            return;
        }

        var dict = _markingManager.MarkingsByCategoryAndSpecies(Category, _species);

        foreach (var (id, marking) in dict)
        {
            var item = MarkingList.AddItem(id);
            item.Metadata = marking.ID;

            if (_markings[Slot].MarkingId == id)
            {
                _ignoreItemSelected = true;
                item.Selected = true;
                _ignoreItemSelected = false;
            }
        }
    }

    private void PopulateColors()
    {
        if (_markings == null
            || !_markingManager.TryGetMarking(_markings[Slot], out var proto))
        {
            return;
        }

        var marking = _markings[Slot];

        ColorSelectorContainer.DisposeAllChildren();
        ColorSelectorContainer.RemoveAllChildren();

        if (marking.MarkingColors.Count != proto.Sprites.Count)
        {
            marking = new Marking(marking.MarkingId, proto.Sprites.Count);
        }

        for (var i = 0; i < marking.MarkingColors.Count; i++)
        {
            var selector = new ColorSelectorSliders();
            selector.Color = marking.MarkingColors[i];

            var colorIndex = i;
            selector.OnColorChanged += color =>
            {
                marking.SetColor(colorIndex, color);
                OnColorChanged!((_slot, marking));
            };
        }
    }

    private void SelectMarking(ItemList.ItemListSelectedEventArgs args)
    {
        if (_ignoreItemSelected)
        {
            return;
        }

        var id = (string) MarkingList[args.ItemIndex].Metadata!;
        if (!_markingManager.Markings.ContainsKey(id))
        {
            throw new ArgumentException("Attempted to select non-existent marking.");
        }

        OnMarkingSelect!((_slot, id));
    }

    // Slot logic

    private void PopulateSlotSelector()
    {
        SlotSelector.Visible = Slot >= 0;
        SlotSelector.Clear();

        if (Slot < 0)
        {
            return;
        }

        for (var i = 0; i < _pointsUsed; i++)
        {
            SlotSelector.AddItem($"Slot {i + 1}", i);

            if (i == _slot)
            {
                SlotSelector.SelectId(i);
            }
        }

        AddButton.Disabled = PointsLeft == 0;
        RemoveButton.Disabled = PointsUsed == 0;
    }
}
