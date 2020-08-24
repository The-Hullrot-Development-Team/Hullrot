﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server.GameObjects.Components.Mobs.Speech
{
    [RegisterComponent]
    public class OwOAccentComponent : Component, IAccentComponent
    {
        public override string Name => "OwOAccent";

        private static readonly IReadOnlyList<string> Faces = new List<string>{
            " (・`ω´・)", " ;;w;;", " owo", " UwU", " >w<", " ^w^"
        }.AsReadOnly();
        private string RandomFace => IoCManager.Resolve<IRobustRandom>().Pick(Faces);

        public string Accentuate(string message)
        {
            return message.Replace("!", RandomFace)
                .Replace("r", "w").Replace("R", "W")
                .Replace("l", "w").Replace("L", "W");
        }
    }
}
