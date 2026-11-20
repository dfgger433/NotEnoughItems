using System.Collections;
using System.Reflection;
using NotEnoughItems;
using UnityEngine;

internal static class TooltipSmoke
{
    internal static void Run()
    {
        var type = typeof(CatalogItem).Assembly.GetType("NotEnoughItems.NativeTooltip")!;
        var parse = type.GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)!;
        object[] Parse(string input) => ((IEnumerable)parse.Invoke(null, new object[] { input })!).Cast<object>().ToArray();
        T Field<T>(object run, string field) => (T)run.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(run)!;
        string Text(object[] runs) => string.Concat(runs.Select(run => Field<string>(run, "Text")));
        void Check(bool pass, string name) { if (!pass) throw new Exception(name); Console.WriteLine("PASS " + name); }
        var runs = Parse("<color=\"red\">警告 <alpha=#88><sprite color=\"#00aaff\" tint=1 index='21'>Details<alpha=#ff><br/>more</color>\n<i>end</i>");
        Check(Text(runs) == "警告 Details\nmore\nend", "localized text and line breaks");
        Check(Field<Color32?>(runs[0], "Color")!.Value.r == 255 && Field<Color32?>(runs[0], "Color")!.Value.g == 0, "quoted named color");
        Check(Field<int>(runs[1], "SpriteIndex") == 21 && Field<bool>(runs[1], "TintSprite") && Field<byte?>(runs[1], "Alpha") == 0x88 && Field<Color32?>(runs[1], "Color")!.Value.b == 255, "sprite index, tint and color; alpha");
        Check(Field<byte?>(runs[3], "Alpha") == 0xff && Field<int>(runs[5], "Font") == 2, "alpha restored, italic");
        Check(Text(Parse("before<unknown attr=\"quoted > inside\">inside</unknown><sprite index='oops'>after")) == "beforeinsideafter", "unknown and invalid complete delimiters only");
        Check(Text(Parse("<unknown\n<i>Next</i>; <1%; A < B; <> end")) == "<unknown\nNext; <1%; A < B; <> end", "bad tag cannot swallow new lines, comparison and placeholder text");
        Check(Text(Parse("x<broken attr='no end <color=#aa7733>y</color>z")) == "x<broken attr='no end yz", "malformed opener cannot absorb a following valid tag");
        Check(Text(Parse("<noparse><b>literal</b></noparse><i>ok</i>")) == "<b>literal</b>ok", "intentional noparse text is inert");
        Check(Text(Parse("<noparse><b>not swallowed</b>")) == "not swallowed", "unclosed noparse recovers");
        var slot = type.GetMethod("ForSlot", BindingFlags.NonPublic | BindingFlags.Static)!;
        var resolve = type.GetMethod("TryGetParts", BindingFlags.NonPublic | BindingFlags.Static)!;
        var virtualItem = new CatalogItem("aaa", "<size=900>Label", "<alpha=#00>description", kind: CatalogItemKind.Virtual);
        var data = ((string, string))slot.Invoke(null, new object?[]{virtualItem.DisplayName, virtualItem, "<i>id", "<sprite index=8>Detail", "<color=red>Shortcut", false, null})!;
        object?[] call = [data.Item2, null];
        Check((bool)resolve.Invoke(null, call)!, "registered tooltip parts survive dispatch");
        object[] Read(string field) => ((IEnumerable)call[1]!.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(call[1])!).Cast<object>().ToArray();
        Check(Text(Read("Title")) == "<size=900>Label" && Text(Read("Body")) == "<i>id\n<alpha=#00>description\n<sprite index=8>Detail" && Text(Read("Shortcuts")) == "<color=red>Shortcut", "provider text, IDs, details and shortcuts stay literal, not markup");
        Check(!(bool)resolve.Invoke(null, new object?[] {new string(data.Item2.ToCharArray()), null})!, "same text cannot forge rich tooltip dispatch");
        var glyphAccess = type.GetMethod("CreateGlyphAccess", BindingFlags.NonPublic | BindingFlags.Static)!;
        Check(glyphAccess.Invoke(null, null) != null, "installed TMP sprite glyph atlas metadata resolved");
    }
}
