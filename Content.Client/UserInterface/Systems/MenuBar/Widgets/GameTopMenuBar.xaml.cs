﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Systems.MenuBar.Widgets
{
    [GenerateTypedNameReferences]
    public sealed partial class GameTopMenuBar : UIWidget
    {
        public GameTopMenuBar()
        {
            RobustXamlLoader.Load(this);
        }
    }
}
