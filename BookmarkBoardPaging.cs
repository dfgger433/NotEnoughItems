using System;
using System.Collections.Generic;

namespace NotEnoughItems;

// A tooltip can be taller than the viewport even when it contains a single recipe.
// Count item rows, not whole sections, so every input of such a recipe is reachable.
internal static class BookmarkBoardPaging
{
    internal static IEnumerable<(int Section, int Start, int Count)> Rows(IReadOnlyList<int> counts, int columns)
    {
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        for (int section = 0; section < counts.Count; section++)
        {
            if (counts[section] == 0) yield return (section, 0, 0);
            for (int start = 0; start < counts[section]; start += columns)
                yield return (section, start, Math.Min(columns, counts[section] - start));
        }
    }
}
