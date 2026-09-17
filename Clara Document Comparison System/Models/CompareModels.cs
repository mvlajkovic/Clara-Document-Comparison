using System;
using System.Collections.Generic;

namespace Clara.DocumentComparison.Models
{
    /// <summary>
    /// One word extracted from a PDF, with its position on the page.
    /// Coordinates are already normalised to 0..1 fractions of the page box,
    /// with the origin in the TOP-left corner (same convention the browser uses),
    /// so the client can multiply them by the rendered page size and nothing else.
    /// </summary>
    public class PdfToken
    {
        public int Page { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }

        /// <summary>Text exactly as it appears in the document.</summary>
        public string Text { get; set; }

        /// <summary>Lower-cased, punctuation-stripped form used for matching.</summary>
        public string Key { get; set; }
    }

    public class PageInfo
    {
        public int Number { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int Rotation { get; set; }
    }

    public class Highlight
    {
        public int Block { get; set; }
        public int Page { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }

        /// <summary>"added", "removed" or "changed".</summary>
        public string Kind { get; set; }
    }

    public class SideResult
    {
        public List<PageInfo> Pages { get; set; }
        public List<Highlight> Highlights { get; set; }
        public int WordCount { get; set; }
    }

    public class ChangeBlock
    {
        public int Id { get; set; }
        public string Kind { get; set; }
        public int LeftPage { get; set; }
        public int RightPage { get; set; }
        public string LeftText { get; set; }
        public string RightText { get; set; }
    }

    public class CompareResult
    {
        /// <summary>The number shown top-right. Produced by the existing algorithm, unchanged.</summary>
        public string Difference { get; set; }

        public double DifferenceValue { get; set; }
        public string Algorithm { get; set; }

        public SideResult Left { get; set; }
        public SideResult Right { get; set; }
        public List<ChangeBlock> Blocks { get; set; }

        public int AddedCount { get; set; }
        public int RemovedCount { get; set; }
        public int ChangedCount { get; set; }

        public string LeftName { get; set; }
        public string RightName { get; set; }
        public long ElapsedMs { get; set; }
    }
}
