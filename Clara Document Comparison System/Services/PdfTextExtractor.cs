using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clara.DocumentComparison.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Clara.DocumentComparison.Services
{
    public class ExtractedDocument
    {
        public List<PageInfo> Pages { get; set; }
        public List<PdfToken> Tokens { get; set; }
    }

    /// <summary>
    /// Pulls words + their positions out of a PDF. PdfPig is used because it gives us a
    /// bounding box per word, which is what makes the side-by-side highlighting possible.
    /// </summary>
    public static class PdfTextExtractor
    {
        public static ExtractedDocument Extract(string filePath)
        {
            var pages = new List<PageInfo>();
            var tokens = new List<PdfToken>();

            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    double pw = page.Width;
                    double ph = page.Height;
                    if (pw <= 0 || ph <= 0) { pw = 612; ph = 792; }

                    pages.Add(new PageInfo
                    {
                        Number = page.Number,
                        Width = pw,
                        Height = ph,
                        Rotation = page.Rotation.Value
                    });

                    IEnumerable<Word> words;
                    try
                    {
                        words = page.GetWords(NearestNeighbourWordExtractor.Instance);
                    }
                    catch
                    {
                        words = page.GetWords();
                    }

                    foreach (var word in words)
                    {
                        string text = (word.Text ?? string.Empty).Trim();
                        if (text.Length == 0) continue;

                        var box = word.BoundingBox;

                        double left = Math.Min(box.Left, box.Right);
                        double right = Math.Max(box.Left, box.Right);
                        double bottom = Math.Min(box.Bottom, box.Top);
                        double top = Math.Max(box.Bottom, box.Top);

                        // PDF space has the origin bottom-left and Y growing upwards.
                        // Flip it so the client can treat Y as "distance from the top".
                        double x = left / pw;
                        double y = 1.0 - (top / ph);
                        double w = (right - left) / pw;
                        double h = (top - bottom) / ph;

                        if (w <= 0 || h <= 0) continue;

                        // A degenerate box (some generators emit zero-height words) would
                        // render as an invisible highlight, so give it a usable minimum.
                        if (h < 0.004) h = 0.004;

                        tokens.Add(new PdfToken
                        {
                            Page = page.Number,
                            X = Clamp(x),
                            Y = Clamp(y),
                            W = Math.Min(w, 1.0),
                            H = Math.Min(h, 1.0),
                            Text = text,
                            Key = Normalise(text)
                        });
                    }
                }
            }

            // Words come out in layout order per page; keep pages in order too.
            tokens = tokens.Where(t => t.Key.Length > 0).ToList();

            return new ExtractedDocument { Pages = pages, Tokens = tokens };
        }

        private static double Clamp(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        /// <summary>
        /// Matching key: case-insensitive, punctuation removed, digits kept.
        /// Two words that differ only in surrounding punctuation are treated as equal,
        /// which stops every comma edit from showing up as a rewritten sentence.
        /// </summary>
        public static string Normalise(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
