using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clara.DocumentComparison.Models;

namespace Clara.DocumentComparison.Services
{
    /// <summary>
    /// Takes two token streams, runs the diff, and turns the raw edit script into
    /// something a viewer can draw: change blocks (for the change list and the
    /// prev/next navigation) and merged rectangles (for the coloured overlays).
    /// </summary>
    public static class DiffBuilder
    {
        // Two words are merged into one highlight rectangle when they sit on the same
        // line and the gap between them is smaller than this fraction of the page width.
        private const double MaxMergeGap = 0.02;

        // How much two boxes must overlap vertically to count as "the same line".
        private const double LineOverlapRatio = 0.5;

        public static void Build(
            List<PdfToken> left,
            List<PdfToken> right,
            out List<Highlight> leftHighlights,
            out List<Highlight> rightHighlights,
            out List<ChangeBlock> blocks)
        {
            var interner = new Dictionary<string, int>(StringComparer.Ordinal);
            int[] a = Intern(left, interner);
            int[] b = Intern(right, interner);

            List<Edit> edits = null;
            Exception failure = null;

            // The divide-and-conquer recursion can go deep on very long documents,
            // so give it a roomy stack of its own rather than the request thread's.
            var worker = new System.Threading.Thread(() =>
            {
                try { edits = MyersDiff.Diff(a, b); }
                catch (Exception ex) { failure = ex; }
            }, 32 * 1024 * 1024);

            worker.Start();
            worker.Join();

            if (failure != null) throw failure;

            var deleted = new bool[a.Length];
            var inserted = new bool[b.Length];
            foreach (var e in edits)
            {
                if (e.Kind == EditKind.Delete) deleted[e.Index] = true;
                else inserted[e.Index] = true;
            }

            leftHighlights = new List<Highlight>();
            rightHighlights = new List<Highlight>();
            blocks = new List<ChangeBlock>();

            int i = 0, j = 0, blockId = 0;

            while (i < a.Length || j < b.Length)
            {
                bool delHere = i < a.Length && deleted[i];
                bool insHere = j < b.Length && inserted[j];

                if (!delHere && !insHere)
                {
                    // Words line up, nothing to draw.
                    i++; j++;
                    continue;
                }

                int aStart = i, bStart = j;
                while (i < a.Length && deleted[i]) i++;
                while (j < b.Length && inserted[j]) j++;

                int removedCount = i - aStart;
                int addedCount = j - bStart;

                string kind = removedCount > 0 && addedCount > 0
                    ? "changed"
                    : (removedCount > 0 ? "removed" : "added");

                var block = new ChangeBlock
                {
                    Id = blockId,
                    Kind = kind,
                    LeftPage = removedCount > 0 ? left[aStart].Page : 0,
                    RightPage = addedCount > 0 ? right[bStart].Page : 0,
                    LeftText = Excerpt(left, aStart, removedCount),
                    RightText = Excerpt(right, bStart, addedCount)
                };

                // A pure insertion still needs somewhere to point to on the left pane,
                // and vice versa, otherwise navigation jumps to page 0.
                if (block.LeftPage == 0)
                    block.LeftPage = aStart > 0 ? left[aStart - 1].Page : (left.Count > 0 ? left[Math.Min(aStart, left.Count - 1)].Page : 1);
                if (block.RightPage == 0)
                    block.RightPage = bStart > 0 ? right[bStart - 1].Page : (right.Count > 0 ? right[Math.Min(bStart, right.Count - 1)].Page : 1);

                blocks.Add(block);

                leftHighlights.AddRange(MergeRun(left, aStart, removedCount, blockId, kind));
                rightHighlights.AddRange(MergeRun(right, bStart, addedCount, blockId, kind));

                blockId++;
            }
        }

        private static int[] Intern(List<PdfToken> tokens, Dictionary<string, int> interner)
        {
            var ids = new int[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                int id;
                if (!interner.TryGetValue(tokens[i].Key, out id))
                {
                    id = interner.Count + 1;
                    interner[tokens[i].Key] = id;
                }
                ids[i] = id;
            }
            return ids;
        }

        private static string Excerpt(List<PdfToken> tokens, int start, int count)
        {
            if (count <= 0) return string.Empty;

            const int maxWords = 24;
            var sb = new StringBuilder();
            int take = Math.Min(count, maxWords);

            for (int k = 0; k < take; k++)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(tokens[start + k].Text);
            }

            if (count > take) sb.Append(" …");
            return sb.ToString();
        }

        /// <summary>
        /// Collapses a run of changed words into one rectangle per line, so a changed
        /// sentence reads as a highlighted sentence rather than a row of separate chips.
        /// </summary>
        private static List<Highlight> MergeRun(List<PdfToken> tokens, int start, int count, int blockId, string kind)
        {
            var result = new List<Highlight>();
            if (count <= 0) return result;

            Highlight current = null;

            for (int k = 0; k < count; k++)
            {
                var t = tokens[start + k];

                if (current != null && CanMerge(current, t))
                {
                    double right = Math.Max(current.X + current.W, t.X + t.W);
                    double bottom = Math.Max(current.Y + current.H, t.Y + t.H);
                    current.X = Math.Min(current.X, t.X);
                    current.Y = Math.Min(current.Y, t.Y);
                    current.W = right - current.X;
                    current.H = bottom - current.Y;
                    continue;
                }

                if (current != null) result.Add(current);

                current = new Highlight
                {
                    Block = blockId,
                    Page = t.Page,
                    X = t.X,
                    Y = t.Y,
                    W = t.W,
                    H = t.H,
                    Kind = kind
                };
            }

            if (current != null) result.Add(current);
            return result;
        }

        private static bool CanMerge(Highlight h, PdfToken t)
        {
            if (h.Page != t.Page) return false;

            double overlap = Math.Min(h.Y + h.H, t.Y + t.H) - Math.Max(h.Y, t.Y);
            double minHeight = Math.Min(h.H, t.H);
            if (minHeight <= 0 || overlap / minHeight < LineOverlapRatio) return false;

            double gap = t.X - (h.X + h.W);
            return gap >= -0.5 && gap < MaxMergeGap;
        }
    }
}
