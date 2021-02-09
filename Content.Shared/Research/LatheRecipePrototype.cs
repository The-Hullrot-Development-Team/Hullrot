using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using YamlDotNet.RepresentationModel;

namespace Content.Shared.Research
{
    [NetSerializable, Serializable, Prototype("latheRecipe")]
    public class LatheRecipePrototype : IPrototype, IIndexedPrototype, IDeepClone
    {
        [YamlField("name")]
        private string _name;
        [YamlField("id")]
        private string _id;
        [YamlField("icon")]
        private SpriteSpecifier _icon = SpriteSpecifier.Invalid;
        [YamlField("description")]
        private string _description;
        [YamlField("result")]
        private string _result;
        [YamlField("completetime")]
        private int _completeTime = 2500;
        [YamlField("materials")]
        private Dictionary<string, int> _requiredMaterials = new();

        [ViewVariables]
        public string ID => _id;

        /// <summary>
        ///     Name displayed in the lathe GUI.
        /// </summary>
        [ViewVariables]
        public string Name
        {
            get
            {
                if (_name.Trim().Length != 0) return _name;
                var protoMan = IoCManager.Resolve<IPrototypeManager>();
                if (protoMan == null) return _description;
                protoMan.TryIndex(_result, out EntityPrototype prototype);
                if (prototype?.Name != null)
                    _name = prototype.Name;
                return _name;
            }
        }

        /// <summary>
        ///     Short description displayed in the lathe GUI.
        /// </summary>
        [ViewVariables]
        public string Description
        {
            get
            {
                if (_description.Trim().Length != 0) return _description;
                var protoMan = IoCManager.Resolve<IPrototypeManager>();
                if (protoMan == null) return _description;
                protoMan.TryIndex(_result, out EntityPrototype prototype);
                if (prototype?.Description != null)
                    _description = prototype.Description;
                return _description;
            }
        }

        /// <summary>
        ///     Texture path used in the lathe GUI.
        /// </summary>
        [ViewVariables]
        public SpriteSpecifier Icon => _icon;

        /// <summary>
        ///     The prototype name of the resulting entity when the recipe is printed.
        /// </summary>
        [ViewVariables]
        public string Result => _result;

        /// <summary>
        ///     The materials required to produce this recipe.
        ///     Takes a material ID as string.
        /// </summary>
        [ViewVariables]
        public Dictionary<string, int> RequiredMaterials
        {
            get => _requiredMaterials;
            private set => _requiredMaterials = value;
        }


        /// <summary>
        ///     How many milliseconds it'll take for the lathe to finish this recipe.
        ///     Might lower depending on the lathe's upgrade level.
        /// </summary>
        [ViewVariables]
        public int CompleteTime => _completeTime;

        public IDeepClone DeepClone()
        {
            return new LatheRecipePrototype
            {
                _name = _name,
                _id = _id,
                _description = _description,
                _icon = IDeepClone.CloneValue(_icon),
                _result = _result,
                _completeTime = _completeTime,
                _requiredMaterials = IDeepClone.CloneValue(_requiredMaterials)
            };
        }
    }
}
