﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.FlavorText
{
    [GenerateTypedNameReferences]
    public sealed partial class FlavorText : Control
    {
        // TODO: Figure out a different way to have CFlavorTextInput accessed that isn't... this
        public LineEdit FlavorTextInput => CFlavorTextInput;

        public Action<string>? OnFlavorTextChanged;

        public FlavorText()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            CFlavorTextInput.OnTextChanged += _ => FlavorTextChanged();
        }

        public void FlavorTextChanged()
        {
            OnFlavorTextChanged?.Invoke(CFlavorTextInput.Text);
        }
    }
}
