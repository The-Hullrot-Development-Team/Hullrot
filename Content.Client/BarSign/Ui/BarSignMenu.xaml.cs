﻿using Content.Client.UserInterface.Controls;
using Content.Shared.BarSign;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.BarSign.Ui;

[GenerateTypedNameReferences]
public sealed partial class BarSignMenu : FancyWindow
{
    private string? _currentId;

    private readonly List<BarSignPrototype> _cachedPrototypes = new();

    public event Action<string>? OnSignSelected;

    public BarSignMenu(BarSignPrototype? currentSign, List<BarSignPrototype> signs)
    {
        RobustXamlLoader.Load(this);
        _currentId = currentSign?.ID;

        _cachedPrototypes.Clear();
        _cachedPrototypes = signs;
        foreach (var proto in _cachedPrototypes)
        {
            SignOptions.AddItem(Loc.GetString(proto.Name));
        }

        SignOptions.OnItemSelected += idx =>
        {
            OnSignSelected?.Invoke(_cachedPrototypes[idx.Id].ID);
            SignOptions.SelectId(idx.Id);
        };

        if (currentSign != null)
        {
            var idx = _cachedPrototypes.IndexOf(currentSign);
            SignOptions.TrySelectId(idx);
        }
    }

    public void UpdateState(BarSignPrototype newSign)
    {
        if (_currentId != null && newSign.ID == _currentId)
            return;
        _currentId = newSign.ID;
        var idx = _cachedPrototypes.IndexOf(newSign);
        SignOptions.TrySelectId(idx);
    }
}
