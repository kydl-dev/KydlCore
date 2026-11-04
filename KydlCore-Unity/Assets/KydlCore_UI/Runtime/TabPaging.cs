using System;
using System.Collections.Generic;

namespace KydlCore.Ui;

/// <summary>
/// The pure rules of the hub's tab rows (PLAN T-035, D-027): a row of tabs wraps onto several lines; up to <see cref="MaxRows"/> lines are shown,
/// with more a scroll bar and two small arrows (the number of the top line between them) let the player move one line at a time. No Unity types
/// and no logging (like WindowStack / HubModel): <see cref="TabRows"/> measures the tabs and does the drawing, this class decides lines and
/// positions. A "line" is counted from 0; `top` is the first shown line.
/// </summary>
internal static class TabPaging
{
    /// <summary>How many lines are shown at once.</summary>
    public const int MaxRows = 2;

    /// <summary>
    /// The top of every line of a wrapped row, from the tops of the tabs in tab order. A tab whose top is more than `tolerance` below the line's
    /// top starts the next line (tabs on one line differ by the lift of the selected one); the line's top is its highest tab.
    /// </summary>
    public static float[] RowTops(IList<float> childTops, float tolerance)
    {
        var rows = new List<float>();
        if (childTops == null) return rows.ToArray();
        foreach (float y in childTops)
        {
            if (rows.Count == 0 || y - rows[rows.Count - 1] > tolerance) rows.Add(y);
            else rows[rows.Count - 1] = Math.Min(rows[rows.Count - 1], y);
        }
        return rows.ToArray();
    }

    /// <summary>The line a tab is on (same rule as <see cref="RowTops"/>); -1 for an index outside the list.</summary>
    public static int RowOf(IList<float> childTops, float tolerance, int index)
    {
        if (childTops == null || index < 0 || index >= childTops.Count) return -1;
        var first = new List<float>();
        for (int i = 0; i <= index; i++) first.Add(childTops[i]);
        return RowTops(first, tolerance).Length - 1;
    }

    /// <summary>True when there are more lines than are shown: the arrows and the scroll bar appear.</summary>
    public static bool Needed(int rowCount)
    {
        return rowCount > MaxRows;
    }

    /// <summary>The biggest `top` (the last page): 0 when everything fits.</summary>
    public static int MaxTop(int rowCount)
    {
        return Math.Max(0, rowCount - MaxRows);
    }

    /// <summary>`top` kept inside 0..MaxTop.</summary>
    public static int Clamp(int top, int rowCount)
    {
        return Math.Max(0, Math.Min(top, MaxTop(rowCount)));
    }

    /// <summary>`top` moved by `delta` lines (an arrow = 1, the wheel = 1 per notch), kept inside the pages.</summary>
    public static int Step(int top, int delta, int rowCount)
    {
        return Clamp(top + delta, rowCount);
    }

    /// <summary>The `top` that shows line `row` while changing the shown lines as little as possible (a selected tab on a hidden line).</summary>
    public static int Reveal(int row, int top, int rowCount)
    {
        int t = Math.Min(top, row);                      // a line above the shown ones: it becomes the first line
        t = Math.Max(t, row - MaxRows + 1);              // a line below: it becomes the last shown line
        return Clamp(t, rowCount);
    }

    /// <summary>How far the lines move up so that line `top` is at the start of the view (0 for line 0 or no lines).</summary>
    public static float Offset(float[] rowTops, int top)
    {
        if (rowTops == null || rowTops.Length == 0) return 0f;
        int t = Math.Max(0, Math.Min(top, rowTops.Length - 1));
        return rowTops[t] - rowTops[0];
    }

    /// <summary>The height of the view: the shown lines from line `top` to the end of line top+1 (`contentHeight` is the end of the last line).</summary>
    public static float ViewHeight(float[] rowTops, float contentHeight, int top)
    {
        if (rowTops == null || rowTops.Length == 0) return contentHeight;
        int first = Math.Max(0, Math.Min(top, rowTops.Length - 1));
        int after = first + MaxRows;
        float end = after < rowTops.Length ? rowTops[after] : contentHeight;
        return Math.Max(0f, end - rowTops[first]);
    }

    /// <summary>Where the scroll bar's thumb starts, 0..1 of the track.</summary>
    public static float ThumbStart(int rowCount, int top)
    {
        return rowCount <= 0 ? 0f : (float)Clamp(top, rowCount) / rowCount;
    }

    /// <summary>How long the scroll bar's thumb is, 0..1 of the track (the shown share of all lines).</summary>
    public static float ThumbLength(int rowCount)
    {
        return Math.Min(1f, (float)MaxRows / Math.Max(1, rowCount));
    }
}
