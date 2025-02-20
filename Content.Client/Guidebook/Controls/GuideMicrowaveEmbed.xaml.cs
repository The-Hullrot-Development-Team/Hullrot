using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Client.Message;
using Content.Client.UserInterface.ControlExtensions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Kitchen;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Guidebook.Controls;

/// <summary>
/// Control for embedding a microwave recipe into a guidebook.
/// </summary>
[UsedImplicitly, GenerateTypedNameReferences]
public sealed partial class GuideMicrowaveEmbed : PanelContainer, IDocumentTag, ISearchableControl
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public GuideMicrowaveEmbed()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Stop;
    }

    public GuideMicrowaveEmbed(string recipe) : this()
    {
        GenerateControl(_prototype.Index<FoodRecipePrototype>(recipe));
    }

    public GuideMicrowaveEmbed(FoodRecipePrototype recipe) : this()
    {
        GenerateControl(recipe);
    }

    public bool CheckMatchesSearch(string query)
    {
        return this.ChildrenContainText(query);
    }

    public void SetHiddenState(bool state, string query)
    {
        Visible = CheckMatchesSearch(query) ? state : !state;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!args.TryGetValue("Recipe", out var id))
        {
            Logger.Error("Recipe embed tag is missing recipe prototype argument");
            return false;
        }

        if (!_prototype.TryIndex<FoodRecipePrototype>(id, out var recipe))
        {
            Logger.Error($"Specified recipe prototype \"{id}\" is not a valid recipe prototype");
            return false;
        }

        GenerateControl(recipe);

        control = this;
        return true;
    }

    private void GenerateHeader(FoodRecipePrototype recipe)
    {
        var entity = _prototype.Index<EntityPrototype>(recipe.Result);

        IconContainer.AddChild(new GuideEntityEmbed(recipe.Result, false, false));
        ResultName.SetMarkup(entity.Name);
        ResultDescription.SetMarkup(entity.Description);
    }

    private void GenerateSolidIngredients(FoodRecipePrototype recipe)
    {
        foreach (var (product, amount) in recipe.IngredientsSolids.OrderByDescending(p => p.Value))
        {
            var ingredient = _prototype.Index<EntityPrototype>(product);

            IngredientsGrid.AddChild(new GuideEntityEmbed(product, false, false));

            // solid name

            var solidNameMsg = new FormattedMessage();
            solidNameMsg.AddMarkupOrThrow(Loc.GetString("guidebook-microwave-solid-name-display", ("ingredient", ingredient.Name)));
            solidNameMsg.Pop();

            var solidNameLabel = new RichTextLabel();
            solidNameLabel.SetMessage(solidNameMsg);

            IngredientsGrid.AddChild(solidNameLabel);

            // solid quantity

            var solidQuantityMsg = new FormattedMessage();
            solidQuantityMsg.AddMarkupOrThrow(Loc.GetString("guidebook-microwave-solid-quantity-display", ("amount", amount)));
            solidQuantityMsg.Pop();

            var solidQuantityLabel = new RichTextLabel();
            solidQuantityLabel.SetMessage(solidQuantityMsg);

            IngredientsGrid.AddChild(solidQuantityLabel);
        }
    }

    private void GenerateLiquidIngredients(FoodRecipePrototype recipe)
    {
        foreach (var (product, amount) in recipe.IngredientsReagents.OrderByDescending(p => p.Value))
        {
            var reagent = _prototype.Index<ReagentPrototype>(product);

            // liquid color

            var liquidColorMsg = new FormattedMessage();
            liquidColorMsg.AddMarkupOrThrow(Loc.GetString("guidebook-microwave-reagent-color-display", ("color", reagent.SubstanceColor)));
            liquidColorMsg.Pop();

            var liquidColorLabel = new RichTextLabel();
            liquidColorLabel.SetMessage(liquidColorMsg);
            liquidColorLabel.HorizontalAlignment = Control.HAlignment.Center;

            IngredientsGrid.AddChild(liquidColorLabel);

            // liquid name

            var liquidNameMsg = new FormattedMessage();
            liquidNameMsg.AddMarkupOrThrow(Loc.GetString("guidebook-microwave-reagent-name-display", ("reagent", reagent.LocalizedName)));
            liquidNameMsg.Pop();

            var liquidNameLabel = new RichTextLabel();
            liquidNameLabel.SetMessage(liquidNameMsg);

            IngredientsGrid.AddChild(liquidNameLabel);

            // liquid quantity

            var liquidQuantityMsg = new FormattedMessage();
            liquidQuantityMsg.AddMarkupOrThrow(Loc.GetString("guidebook-microwave-reagent-quantity-display", ("amount", amount)));
            liquidQuantityMsg.Pop();

            var liquidQuantityLabel = new RichTextLabel();
            liquidQuantityLabel.SetMessage(liquidQuantityMsg);

            IngredientsGrid.AddChild(liquidQuantityLabel);
        }
    }

    private void GenerateIngredients(FoodRecipePrototype recipe)
    {
        GenerateLiquidIngredients(recipe);
        GenerateSolidIngredients(recipe);
    }

    private void GenerateCookTime(FoodRecipePrototype recipe)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("guidebook-microwave-cook-time", ("time", recipe.CookTime)));
        msg.Pop();

        CookTimeLabel.SetMessage(msg);
    }

    private void GenerateControl(FoodRecipePrototype recipe)
    {
        GenerateHeader(recipe);
        GenerateIngredients(recipe);
        GenerateCookTime(recipe);
    }
}
