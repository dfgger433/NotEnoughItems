using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace NotEnoughItems;

internal sealed partial class NeiWindow
{
    /// <summary>IMGUI control name for the editable catalog group name field.</summary>
    internal const string CatalogGroupControl = "NEI.CatalogGroup";
    private const float CatalogGroupsSaveDelay = .6f;
    private readonly CatalogGroups _catalogGroups = new();
    private ConfigEntry<string>? _catalogGroupsSetting;
    private Rect _catalogGroupRect;
    private string _catalogGroupEditText = string.Empty;
    private bool _catalogGroupEditing;
    private bool _catalogGroupsDirty;
    private float _catalogGroupsSaveAt;

    /// <summary>Binds the group save and restores the selected group's query. Call once from the window constructor.</summary>
    internal void InitCatalogGroups(ConfigFile config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (_catalogGroupsSetting != null) return;
        _catalogGroupsSetting = config.Bind("NotEnoughItems", "CatalogGroups", string.Empty,
            "Named item-catalog searches and the selected group. Managed by NEI.");
        if (!_catalogGroups.Load(_catalogGroupsSetting.Value))
        {
            _catalogGroups.Reset(DefaultCatalogGroupName(1));
            SaveCatalogGroupsNow();
        }
        // The catalog is refreshed after construction; restoring the query here is enough for that first filter.
        _query = _catalogGroups.Current.Query;
        _page = 0;
    }

    /// <summary>Records a right-hand catalog search in the selected group. The lead calls this when the search changes.</summary>
    internal void SaveCatalogQuery(string query)
    {
        if (_catalogGroups.Count == 0) return;
        if (!_catalogGroups.SetQuery(_catalogGroups.SelectedIndex, query)) return;
        _catalogGroupsDirty = true;
        _catalogGroupsSaveAt = Time.unscaledTime + CatalogGroupsSaveDelay;
    }

    /// <summary>Draws the group cycle button, editable name, and add/remove buttons inside the supplied layout slots.</summary>
    internal void DrawCatalogGroups(Rect selector, Rect groupName, Rect add, Rect remove)
    {
        _catalogGroupRect = groupName;
        if (_catalogGroups.Count == 0) return;
        FlushCatalogGroups();

        var e = Event.current;
        bool focused = GUI.GetNameOfFocusedControl() == CatalogGroupControl;
        if (_catalogGroupEditing && (!focused || (e.type == EventType.MouseDown && !groupName.Contains(e.mousePosition))))
            CommitCatalogGroupName(stayOnFailure: false);

        if (NeiSkin.Button(selector, L("G", "组")))
            SelectCatalogGroup(_catalogGroups.NextIndex);
        if (selector.Contains(e.mousePosition) && selector.width > 0 && selector.height > 0)
        {
            int selected = _catalogGroups.SelectedIndex;
            string body = _catalogGroups.Count > 1
                ? L("Click for the next group: " + _catalogGroups[_catalogGroups.NextIndex].Name,
                    "点击切换到下一组：" + _catalogGroups[_catalogGroups.NextIndex].Name)
                : L("Add another group with +", "用 + 添加其他分组");
            Tip(L($"Catalog group {selected + 1}/{_catalogGroups.Count}: {_catalogGroups.Current.Name}",
                $"目录分组 {selected + 1}/{_catalogGroups.Count}：{_catalogGroups.Current.Name}"), body);
        }

        if (NeiSkin.IconButton(add, "add", L("Add catalog group", "添加目录分组")))
            AddCatalogGroup();
        bool canRemove = _catalogGroups.Count > 1;
        if (NeiSkin.IconButton(remove, "remove",
                canRemove ? L("Remove this catalog group", "删除当前目录分组") : L("Keep at least one catalog group", "至少保留一个目录分组"),
                canRemove))
            RemoveCatalogGroup();

        string text = _catalogGroupEditing ? _catalogGroupEditText : _catalogGroups.Current.Name;
        string input = NeiSkin.SearchField(groupName, text, CatalogGroupControl, CatalogGroups.MaxNameLength);
        bool fieldFocused = GUI.GetNameOfFocusedControl() == CatalogGroupControl;
        if (fieldFocused)
        {
            if (!_catalogGroupEditing)
            {
                _catalogGroupEditing = true;
                _catalogGroupEditText = _catalogGroups.Current.Name;
            }
            else _catalogGroupEditText = input;
        }
        else if (_catalogGroupEditing)
            CommitCatalogGroupName(stayOnFailure: false);

        if (e.type == EventType.KeyDown && fieldFocused &&
            (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            CommitCatalogGroupName(stayOnFailure: true);
            e.Use();
        }
        if (groupName.Contains(e.mousePosition) && !fieldFocused && groupName.width > 20)
            Tip(L("Catalog group name · Enter or click away to save", "目录分组名称 · 回车或点击别处保存"), null);
    }

    private void SelectCatalogGroup(int index)
    {
        if (!_catalogGroups.Select(index)) return;
        ApplyCatalogGroupState();
        SaveCatalogGroupsNow();
    }

    private void AddCatalogGroup()
    {
        if (!_catalogGroups.Add(NextCatalogGroupName())) return;
        ApplyCatalogGroupState();
        SaveCatalogGroupsNow();
    }

    private void RemoveCatalogGroup()
    {
        if (!_catalogGroups.RemoveSelected()) return;
        ApplyCatalogGroupState();
        SaveCatalogGroupsNow();
    }

    // Every selection change replaces the active search, so the grid filters by the saved query immediately.
    private void ApplyCatalogGroupState()
    {
        _catalogGroupEditing = false;
        _catalogGroupEditText = string.Empty;
        _query = _catalogGroups.Current.Query;
        _page = 0;
        ApplyFilter();
    }

    private bool CommitCatalogGroupName(bool stayOnFailure)
    {
        if (!_catalogGroupEditing) return true;
        int index = _catalogGroups.SelectedIndex;
        string before = _catalogGroups[index].Name;
        var result = _catalogGroups.Rename(index, _catalogGroupEditText, out string renamed);
        if (result == CatalogGroupRenameResult.Renamed)
        {
            _catalogGroupEditing = false;
            _catalogGroupEditText = string.Empty;
            GUI.FocusControl(null);
            if (!string.Equals(before, renamed, StringComparison.Ordinal)) SaveCatalogGroupsNow();
            return true;
        }

        Status(result switch
        {
            CatalogGroupRenameResult.Empty => L("A catalog group name cannot be empty.", "目录分组名称不能为空。"),
            CatalogGroupRenameResult.Duplicate => L("A catalog group with that name already exists.", "已存在同名目录分组。"),
            _ => L($"Catalog group names are limited to {CatalogGroups.MaxNameLength} characters.",
                $"目录分组名称最多 {CatalogGroups.MaxNameLength} 个字符。")
        });
        if (stayOnFailure) GUI.FocusControl(CatalogGroupControl);
        else
        {
            _catalogGroupEditing = false;
            _catalogGroupEditText = string.Empty;
            GUI.FocusControl(null);
        }
        return false;
    }

    private string NextCatalogGroupName()
    {
        for (int ordinal = 1; ; ordinal++)
        {
            string candidate = DefaultCatalogGroupName(ordinal);
            if (_catalogGroups.IsNameAvailable(candidate)) return candidate;
        }
    }

    private string DefaultCatalogGroupName(int ordinal)
    {
        if (!_plugin.Chinese) return "Group " + ordinal.ToString(CultureInfo.InvariantCulture);
        string[] numerals = { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
        return ordinal >= 1 && ordinal <= numerals.Length
            ? numerals[ordinal - 1] + "组"
            : "第" + ordinal.ToString(CultureInfo.InvariantCulture) + "组";
    }

    private void FlushCatalogGroups()
    {
        if (!_catalogGroupsDirty || Time.unscaledTime < _catalogGroupsSaveAt) return;
        SaveCatalogGroupsNow();
    }

    private void SaveCatalogGroupsNow()
    {
        if (_catalogGroupsSetting == null || _catalogGroups.Count == 0) return;
        string data = _catalogGroups.Serialize();
        _catalogGroupsDirty = false;
        if (string.Equals(data, _catalogGroupsSetting.Value, StringComparison.Ordinal)) return;
        _catalogGroupsSetting.Value = data;
    }
}
