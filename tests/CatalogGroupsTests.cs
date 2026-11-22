using System;

using NotEnoughItems;

/// <summary>Focused tests for the catalog-group model. Run through the existing console test project.</summary>
/// <remarks>Add ../CatalogGroups.cs and this file to tests/Nei.Tests.csproj, then call CatalogGroupsTests.Run().</remarks>
internal static class CatalogGroupsTests
{
    internal static void Run()
    {
        Action[] checks =
        [
            DefaultGroupCannotBeRemoved,
            QueriesSwitchIndependently,
            RenamesTrimAndRejectInvalidNames,
            QueriesRoundTripDelimitersAndWhitespace,
            MalformedSavesAreRejectedAtomically,
            SelectionAndLimitsStayCoherent,
        ];

        var failures = new System.Collections.Generic.List<Exception>();
        foreach (Action check in checks)
        {
            try { check(); }
            catch (Exception error) { failures.Add(new InvalidOperationException(check.Method.Name, error)); }
        }
        if (failures.Count > 0) throw new AggregateException("Catalog group checks failed.", failures);
    }

    private static void DefaultGroupCannotBeRemoved()
    {
        var groups = new CatalogGroups();
        groups.Reset("一组");
        Check(groups.Count == 1 && groups.SelectedIndex == 0 && groups.Current.Name == "一组" && groups.Current.Query == "",
            "A fresh catalog starts with one empty, selected group.");
        Check(!groups.RemoveSelected() && groups.Count == 1, "The last catalog group cannot be removed.");
        Check(groups.NextIndex == 0, "A single group cycles to itself.");

        groups.Add("二组", "steel");
        Check(groups.Count == 2 && groups.SelectedIndex == 1 && groups.Current.Name == "二组" && groups.Current.Query == "steel",
            "Adding a group appends and selects it.");
        Check(groups.RemoveSelected() && groups.Count == 1 && groups.SelectedIndex == 0 && groups.Current.Name == "一组",
            "Removing the selected group falls back to the remaining group.");
        Check(!groups.RemoveSelected(), "The remaining group still cannot be removed.");
    }

    private static void QueriesSwitchIndependently()
    {
        var groups = new CatalogGroups();
        groups.Reset("一组");
        groups.Add("二组");
        groups.Add("三组");
        Check(groups.Count == 3 && groups.SelectedIndex == 2, "Fixture adds and selects each group.");
        Check(groups.SetQuery(0, "@forge") && groups.SetQuery(1, "steel -brass"),
            "Each group stores its own query.");
        Check(!groups.SetQuery(2, string.Empty), "Re-recording an already empty query reports no change.");
        Check(groups.Select(0) && groups.Current.Query == "@forge", "Selecting the first group restores its query.");
        Check(groups.NextIndex == 1 && groups.Select(groups.NextIndex) && groups.Current.Query == "steel -brass",
            "The cycle button moves to the next group's saved query.");
        Check(!groups.SetQuery(1, "steel -brass"), "Re-recording the same query reports no change.");
        Check(groups.SetQuery(1, "cloth") && groups.Current.Query == "cloth", "A group query can be replaced.");

        string data = groups.Serialize();
        var restored = new CatalogGroups();
        Check(restored.Load(data), "The group payload round-trips.");
        Check(restored.Count == 3 && restored.SelectedIndex == 1 && restored[0].Query == "@forge" &&
            restored[1].Query == "cloth" && restored[2].Query == string.Empty &&
            restored.Select(2) && restored.Current.Query == string.Empty,
            "Queries and the selected group survive reload independently.");

        Check(groups.RemoveSelected() && groups.Count == 2 && groups.SelectedIndex == 1 &&
            groups.Current.Name == "三组" && groups.Current.Query == "",
            "Deleting a group selects a group with its own query.");
    }

    private static void RenamesTrimAndRejectInvalidNames()
    {
        var groups = new CatalogGroups();
        groups.Reset("一组");
        groups.Add("二组");
        groups.Add("三组");
        Check(groups.Rename(1, " 工作台 ", out string renamed) == CatalogGroupRenameResult.Renamed && renamed == "工作台" &&
            groups[1].Name == "工作台", "Renaming trims and commits the new name.");
        Check(groups.Rename(1, "三组", out _) == CatalogGroupRenameResult.Duplicate, "Another group's name is rejected.");
        Check(groups.Rename(0, "", out _) == CatalogGroupRenameResult.Empty &&
            groups.Rename(0, " \t ", out _) == CatalogGroupRenameResult.Empty, "Empty and whitespace-only names are rejected.");
        Check(groups.Rename(0, new string('a', CatalogGroups.MaxNameLength + 1), out _) == CatalogGroupRenameResult.Invalid,
            "Oversized names are rejected.");
        Check(groups.Rename(0, "bad\nname", out _) == CatalogGroupRenameResult.Invalid, "Control characters are rejected.");
        Check(groups.Rename(0, "一组", out _) == CatalogGroupRenameResult.Renamed && groups[0].Name == "一组",
            "Renaming a group to its own name is allowed.");
        Check(groups.Rename(0, "一组 ", out _) == CatalogGroupRenameResult.Renamed && groups[0].Name == "一组",
            "Renaming to the same trimmed name changes nothing.");
        Check(groups.Rename(0, "WORK", out _) == CatalogGroupRenameResult.Renamed &&
            groups.Rename(1, "work", out _) == CatalogGroupRenameResult.Duplicate,
            "Duplicate detection is case-insensitive.");
        Check(groups.Rename(-1, "x", out _) == CatalogGroupRenameResult.Invalid,
            "Renaming a missing group is rejected.");
    }

    private static void QueriesRoundTripDelimitersAndWhitespace()
    {
        var groups = new CatalogGroups();
        groups.Reset("炉:料|%😀");
        const string query = "@\"forge guild\" steel | -brass  #heat resistant  %20 \\";
        Check(groups.SetQuery(0, query), "A query with separators, percent signs, Unicode, and whitespace is stored verbatim.");
        groups.Add("work:0|%");
        Check(groups.SetQuery(1, "  ") && groups[1].Query == "  ", "Whitespace-only queries are valid saved searches.");

        string data = groups.Serialize();
        var restored = new CatalogGroups();
        Check(restored.Load(data), "Escaped names and queries load.");
        Check(restored.Count == 2 && restored[0].Name == "炉:料|%😀" && restored[0].Query == query &&
            restored[1].Name == "work:0|%" && restored[1].Query == "  ", "Escaped values keep their exact content.");
        Check(restored.Serialize() == data, "Serialization is stable after a full round-trip.");
    }

    private static void MalformedSavesAreRejectedAtomically()
    {
        var groups = new CatalogGroups();
        groups.Reset("kept");
        groups.Add("other", "query");
        string before = groups.Serialize();

        string[] invalid =
        [
            null!,
            "",
            "cg2|0|a|b",
            "cg1|0|a",
            "cg1|x|a|b",
            "cg1|-1|a|b",
            "cg1|0|a%FF|b",
            "cg1|0|a%|b",
            "cg1|0|a|b%0A",
            "cg1|0|%20|b",
            "cg1|0|a|b|a|b",
            "cg1|0|a|b|" + new string('c', CatalogGroups.MaxNameLength + 1) + "|d",
            "cg1|0|a|b|extra",
        ];
        foreach (string data in invalid)
        {
            Check(!groups.Load(data), $"Reject damaged payload: {data ?? "<null>"}.");
            Check(groups.Serialize() == before, "A rejected payload leaves the current groups unchanged.");
        }

        var one = new CatalogGroups();
        Check(one.Load("cg1|0|kept|") && one.Count == 1 && one.SelectedIndex == 0 &&
            one.Current.Name == "kept" && one.Current.Query == "", "An empty escaped query loads as an empty search.");
    }

    private static void SelectionAndLimitsStayCoherent()
    {
        var groups = new CatalogGroups();
        groups.Reset("one");
        groups.Add("two");
        groups.Add("three");
        Check(!groups.Select(3) && !groups.Select(-1) && groups.SelectedIndex == 2, "Out-of-range selections are ignored.");
        Check(groups.Select(0) && !groups.Select(0), "Selecting the current group reports no change.");
        Check(!groups.Add("two") && !groups.Add("") && !groups.Add("bad\tname"), "Invalid group additions leave the model unchanged.");
        Check(!groups.Add("new", new string('q', CatalogGroups.MaxQueryLength + 1)), "Oversized queries are rejected.");
        Check(groups.Add("new", new string('q', CatalogGroups.MaxQueryLength)) && groups.Count == 4,
            "Queries at the documented limit are accepted.");
        Check(groups.NextIndex == 0 && groups.Select(groups.NextIndex) && groups.SelectedIndex == 0,
            "The cycle wraps from the last group to the first.");

        var loaded = new CatalogGroups();
        Check(loaded.Load("cg1|99|a|b") && loaded.SelectedIndex == 0, "An out-of-range saved selection is clamped.");
        Check(loaded.Load("cg1|0|%20trimmed%20|q") && loaded.Current.Name == "trimmed",
            "Saved names are trimmed like edited names.");
        Check(!groups.IsNameAvailable("TWO") && groups.IsNameAvailable("two", 1) && !groups.IsNameAvailable(" "),
            "Availability checks use the same trim and duplicate rules.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
