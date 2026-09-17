using System;
using System.Collections.Generic;

namespace Clara.DocumentComparison.Services
{
    public enum EditKind { Delete, Insert }

    public struct Edit
    {
        public EditKind Kind;
        public int Index;   // index into A for Delete, into B for Insert
    }

    /// <summary>
    /// Myers' O(ND) difference algorithm, linear-space (divide and conquer on the
    /// middle snake). Produces a minimal edit script, which is what makes the
    /// highlighting tight instead of "this whole paragraph changed".
    ///
    /// Sequences are int arrays: each word is interned to an id beforehand, so
    /// comparisons are integer compares rather than string compares.
    /// </summary>
    public static class MyersDiff
    {
        public static List<Edit> Diff(int[] a, int[] b)
        {
            var result = new List<Edit>();
            Recurse(a, 0, a.Length, b, 0, b.Length, result);
            return result;
        }

        private static void Recurse(int[] a, int aLo, int aHi, int[] b, int bLo, int bHi, List<Edit> result)
        {
            // Trim the matching head and tail: cheap, and it makes the snake search tiny.
            while (aLo < aHi && bLo < bHi && a[aLo] == b[bLo]) { aLo++; bLo++; }
            while (aLo < aHi && bLo < bHi && a[aHi - 1] == b[bHi - 1]) { aHi--; bHi--; }

            int n = aHi - aLo;
            int m = bHi - bLo;

            if (n == 0)
            {
                for (int j = bLo; j < bHi; j++) result.Add(new Edit { Kind = EditKind.Insert, Index = j });
                return;
            }

            if (m == 0)
            {
                for (int i = aLo; i < aHi; i++) result.Add(new Edit { Kind = EditKind.Delete, Index = i });
                return;
            }

            if (n == 1 && m == 1)
            {
                result.Add(new Edit { Kind = EditKind.Delete, Index = aLo });
                result.Add(new Edit { Kind = EditKind.Insert, Index = bLo });
                return;
            }

            int x1, y1, x2, y2;
            MiddleSnake(a, aLo, aHi, b, bLo, bHi, out x1, out y1, out x2, out y2);

            Recurse(a, aLo, x1, b, bLo, y1, result);
            Recurse(a, x2, aHi, b, y2, bHi, result);
        }

        private static void MiddleSnake(int[] a, int aLo, int aHi, int[] b, int bLo, int bHi,
                                        out int x1, out int y1, out int x2, out int y2)
        {
            int n = aHi - aLo;
            int m = bHi - bLo;
            int max = n + m;
            int delta = n - m;
            bool odd = (delta & 1) != 0;
            int off = max + 1;
            int size = 2 * max + 3;

            var vf = new int[size];
            var vr = new int[size];
            vf[off + 1] = 0;
            vr[off + 1] = 0;

            int limit = (max + 1) / 2;

            for (int d = 0; d <= limit; d++)
            {
                // Forward pass.
                for (int k = -d; k <= d; k += 2)
                {
                    int idx = off + k;
                    int x = (k == -d || (k != d && vf[idx - 1] < vf[idx + 1]))
                        ? vf[idx + 1]
                        : vf[idx - 1] + 1;
                    int y = x - k;

                    int sx = x, sy = y;
                    while (x < n && y < m && a[aLo + x] == b[bLo + y]) { x++; y++; }
                    vf[idx] = x;

                    if (odd)
                    {
                        int kr = delta - k;
                        if (kr >= -(d - 1) && kr <= (d - 1) && x + vr[off + kr] >= n)
                        {
                            x1 = aLo + sx; y1 = bLo + sy;
                            x2 = aLo + x; y2 = bLo + y;
                            return;
                        }
                    }
                }

                // Reverse pass. x counts elements consumed from the end of A.
                for (int k = -d; k <= d; k += 2)
                {
                    int idx = off + k;
                    int x = (k == -d || (k != d && vr[idx - 1] < vr[idx + 1]))
                        ? vr[idx + 1]
                        : vr[idx - 1] + 1;
                    int y = x - k;

                    int sx = x, sy = y;
                    while (x < n && y < m && a[aHi - 1 - x] == b[bHi - 1 - y]) { x++; y++; }
                    vr[idx] = x;

                    if (!odd)
                    {
                        int kf = delta - k;
                        if (kf >= -d && kf <= d && x + vf[off + kf] >= n)
                        {
                            // Translate back into forward coordinates.
                            x1 = aLo + n - x; y1 = bLo + m - y;
                            x2 = aLo + n - sx; y2 = bLo + m - sy;
                            return;
                        }
                    }
                }
            }

            // Unreachable for well-formed input; fall back to "replace everything".
            x1 = aLo; y1 = bLo; x2 = aHi; y2 = bHi;
        }
    }
}
