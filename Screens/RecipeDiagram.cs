using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    private const float RecipeDiagramBottom = 7f;
    private const float RecipeChoiceBottom = 6f;

    private int _diagramRevision = -1;
    private int _nextRequirementToken = 1;
    private readonly Dictionary<(string Recipe, int Ingredient), int> _requirementTokens = new();
    private readonly Dictionary<int, (string Recipe, int Ingredient)> _requirementByToken = new();
    private readonly Dictionary<(string Recipe, int Ingredient), (IReadOnlyList<CatalogItem> Related, IReadOnlyList<CatalogItem> Eligible)> _diagramAlternatives = new();
    private int _candidateCycleStep;
    private float _nextCandidateCycle;
    private const float CandidateCycleSeconds = 1f;

    private readonly struct RecipeDiagramPlan
    {
        internal RecipeDiagramPlan(RecipeGridLayout grid, IReadOnlyList<CatalogItem> choices)
        {
            Grid = grid;
            Choices = choices;
        }

        internal RecipeGridLayout Grid { get; }
        internal IReadOnlyList<CatalogItem> Choices { get; }
        internal float ChoicesY => Grid.Plate.yMax + 5f;
        internal float ExtraHeight => Choices.Count == 0 ? 0f :
            5f + RecipeGridLayout.RowCount(Choices.Count, Grid.ChoiceColumns) * Grid.Cell + RecipeChoiceBottom;
    }

    private void CollapseRecipeRequirement() => _expandedRequirement = -1;

    private RecipeDiagramPlan BuildRecipeDiagramPlan(Recipe recipe, Rect area)
    {
        var grid = new RecipeGridLayout(area, recipe.Inputs.Count, recipe.Outputs.Count, IconScale);
        IReadOnlyList<CatalogItem> choices = Array.Empty<CatalogItem>();
        if (TryExpandedInput(recipe, out int expandedIndex, out var ingredient))
            choices = DiagramAlternatives(recipe, expandedIndex, ingredient).Related;
        return new RecipeDiagramPlan(grid, choices);
    }

    private void DrawRecipeDiagram(Rect area, RecipeDiagramPlan plan, Recipe recipe, Rect viewport, Vector2 offset)
    {
        var grid = plan.Grid;
        NeiSkin.RecipeArrow(grid.Arrow);
        if (RecipeListLayout.VisibleHit(grid.Arrow, viewport, offset).Contains(Event.current.mousePosition + offset))
            Tip(recipe.Name, recipe.Description);

        var clip = new Rect(viewport.position - offset, viewport.size);
        var inputRange = grid.VisibleInputs(clip);
        for (int i = inputRange.Start; i < inputRange.End; i++)
        {
            Rect slot = grid.Input(i);
            if (i >= recipe.Inputs.Count)
            {
                NeiSkin.RecipeSlot(slot);
                continue;
            }
            var input = recipe.Inputs[i];
            var (related, eligible) = input.MatchesAny ? DiagramAlternatives(recipe, i, input) :
                (Array.Empty<CatalogItem>(), Array.Empty<CatalogItem>());
            // Keep the normal rotation even if the catalog only has below-threshold examples.
            var candidates = eligible.Count > 0 ? eligible : related;
            CatalogItem? display = input.MatchesAny
                ? CycleCandidate(recipe, i, candidates)
                : _catalog.TryGetItem(input.BrowseId, out var exact) ? exact : null;
            int token = related.Count > 0 ? RequirementToken(recipe, i) : -1;
            string title = input.MatchesAny ? RequirementTitle(input) :
                !string.IsNullOrWhiteSpace(input.DisplayName) ? input.DisplayName : Display(input.BrowseId);
            string details = input.IsCatalyst ? L("Tool / not consumed", "工具／不消耗") : L("Consumed by this recipe", "配方会消耗此原料");
            if (input.MatchesAny)
            {
                if (eligible.Count == 0)
                    details += "\n" + L("No catalog items meet the requirement", "目录中没有符合要求的物品");
                else if (eligible.Count == 1)
                    details += "\n" + L("1 supported item type", "支持 1 种物品");
                if (related.Count > 1)
                    details += "\n" + L("Left-click to expand candidates", "左键展开候选列表");
            }
            if (input.MinimumCondition > 0 && !input.IsLiquid)
                details += "\n" + L("Minimum condition: ", "最低耐久：") + input.MinimumCondition.ToString("P0", CultureInfo.InvariantCulture);
            if (input.NoMaxCondition && !input.IsLiquid)
                details += "\n" + L("Must not be at full condition", "不可为满耐久");

            ItemSlot(slot, display?.BrowseId ?? input.BrowseId, clipping: viewport, globalOffset: offset,
                amount: DiagramAmount(input, display), fallback: display?.DisplayName ?? title,
                details: details, requirement: token, belowRequirement: display != null && input.MatchesAny && !input.Matches(display),
                corner: input.IsCatalyst ? L("T", "工具") : null, diagram: true,
                candidates: input.MatchesAny ? eligible : null, candidateRequirement: input.MatchesAny ? title : null,
                iconOnly: input.MatchesAny && display != null, requirementIngredient: input);
        }

        var outputRange = grid.VisibleOutputs(clip, recipe.Outputs.Count);
        for (int i = outputRange.Start; i < outputRange.End; i++)
        {
            if (i >= recipe.Outputs.Count)
            {
                NeiSkin.RecipeSlot(grid.Output(i));
                continue;
            }
            var output = recipe.Outputs[i];
            string? details = !output.IsLiquid && output.Condition < 1f
                ? L("Condition: ", "耐久：") + output.Condition.ToString("P0", CultureInfo.InvariantCulture)
                : null;
            ItemSlot(grid.Output(i), output.BrowseId, amount: Amount(output.Amount, output.IsLiquid),
                fallback: output.DisplayName, clipping: viewport, globalOffset: offset, details: details, diagram: true);
        }

        if (plan.Choices.Count > 0 && TryExpandedInput(recipe, out int expanded, out var requirement))
        {
            float choicesY = plan.ChoicesY;
            var choiceRange = grid.VisibleChoices(clip, choicesY, plan.Choices.Count);
            for (int i = choiceRange.Start; i < choiceRange.End; i++)
            {
                var candidate = plan.Choices[i];
                ItemSlot(grid.Choice(i, area.x + 3, choicesY), candidate.BrowseId,
                    clipping: viewport, globalOffset: offset, amount: DiagramAmount(requirement, candidate),
                    details: !requirement.Matches(candidate)
                        ? L("Has this quality, but below this recipe's requirement", "具有该性质，但未达本配方要求")
                        : null,
                    belowRequirement: !requirement.Matches(candidate), diagram: true, iconOnly: true);
            }
        }

    }

    private static SlotQuantity? DiagramAmount(RecipeIngredient input, CatalogItem? display)
    {
        if (!input.MatchesAny) return Amount(input.Amount, input.IsLiquid);
        return LiquidInputVolume(input, display) is { } volume ? Amount(volume, true) : null;
    }

    private static double? LiquidInputVolume(RecipeIngredient input, CatalogItem? candidate)
    {
        if (!input.IsLiquid || candidate == null || !input.Matches(candidate)) return null;
        if (input.QualityId.Length == 0) return input.Amount;
        float strength = candidate.Qualities.Where(quality => SameId(quality.id, input.QualityId)).Max(quality => quality.amount);
        return (double)input.Amount / strength;
    }

    private void EnsureDiagramRevision()
    {
        int revision = _catalog.Revision;
        if (_diagramRevision == revision) return;
        if (_diagramRevision >= 0) CollapseRecipeRequirement();
        _diagramRevision = revision;
        _requirementTokens.Clear();
        _requirementByToken.Clear();
        _diagramAlternatives.Clear();
        _favoriteCandidates.Clear();
        _nextRequirementToken = 1;
    }

    private int RequirementToken(Recipe recipe, int ingredient)
    {
        var key = (RecipeKey(recipe), ingredient);
        if (_requirementTokens.TryGetValue(key, out int token)) return token;
        if (_nextRequirementToken == int.MaxValue)
        {
            _requirementTokens.Clear();
            _requirementByToken.Clear();
            _nextRequirementToken = 1;
            CollapseRecipeRequirement();
        }
        token = _nextRequirementToken++;
        _requirementTokens.Add(key, token);
        _requirementByToken.Add(token, key);
        return token;
    }

    private bool TryExpandedInput(Recipe recipe, out int index, out RecipeIngredient ingredient)
    {
        EnsureDiagramRevision();
        if (_expandedRequirement > 0 && _requirementByToken.TryGetValue(_expandedRequirement, out var target) &&
            string.Equals(target.Recipe, RecipeKey(recipe), StringComparison.Ordinal) &&
            target.Ingredient >= 0 && target.Ingredient < recipe.Inputs.Count && recipe.Inputs[target.Ingredient].MatchesAny)
        {
            index = target.Ingredient;
            ingredient = recipe.Inputs[index];
            return true;
        }
        index = -1;
        ingredient = null!;
        return false;
    }

    private (IReadOnlyList<CatalogItem> Related, IReadOnlyList<CatalogItem> Eligible) DiagramAlternatives(
        Recipe recipe, int index, RecipeIngredient ingredient)
    {
        var key = (RecipeKey(recipe), index);
        if (!_diagramAlternatives.TryGetValue(key, out var matches))
        {
            var related = _catalog.GetMatchingItems(ingredient, includeBelowRequirement: true);
            var eligible = related.Where(ingredient.Matches).ToArray();
            matches = (related, eligible);
            _diagramAlternatives.Add(key, matches);
        }
        return matches;
    }

    private readonly Dictionary<(string Recipe, int Index), CatalogItem[]> _favoriteCandidates = new();

    private CatalogItem? CycleCandidate(Recipe recipe, int index, IReadOnlyList<CatalogItem> eligible)
    {
        if (eligible.Count == 0) return null;
        var ordered = OrderedCandidates(recipe, index, eligible);
        return ordered[RecipeGridLayout.CandidateIndex(_candidateCycleStep, ordered.Length)];
    }

    private CatalogItem[] OrderedCandidates(Recipe recipe, int index, IReadOnlyList<CatalogItem> eligible)
    {
        var key = (RecipeKey(recipe), index);
        if (!_favoriteCandidates.TryGetValue(key, out var ordered))
        {
            // Keep a preferred material first, but never hide other valid types from
            // the preview. A saved route is a hint, not a restriction on alternatives.
            _favoriteCandidates.Add(key, ordered = eligible.OrderByDescending(item => PreferredMaterials.Contains(item.BrowseId)).ToArray());
        }
        return ordered;
    }

    private void AdvanceRecipeCandidates()
    {
        // Advance only on repaint so input always targets the icon actually shown. Holding
        // Shift freezes the display while the player moves from a requirement to its popup.
        float now = Time.unscaledTime;
        if (SelectionShiftHeld || _nextCandidateCycle == 0f)
            _nextCandidateCycle = now + CandidateCycleSeconds;
        else if (now >= _nextCandidateCycle)
        {
            _candidateCycleStep = _candidateCycleStep == int.MaxValue ? 0 : _candidateCycleStep + 1;
            _nextCandidateCycle = now + CandidateCycleSeconds;
        }
    }
}
