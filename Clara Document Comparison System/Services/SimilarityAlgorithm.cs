using System;

namespace Clara.DocumentComparison.Services
{
    /// <summary>
    /// The number shown in the top-right corner. This calls your original code path
    /// exactly, unmodified:
    ///   Reader.uniqueWordsForApi(path)             -> word-frequency dictionary
    ///   Reader.removeTopN(dict, 10, 15)             -> your trimming rule
    ///   LevenshteinDistance.distanceMeasure(.., 2)  -> Manhattan
    ///   (1 - distance) * 100                        -> percentage
    ///
    /// This intentionally re-parses each PDF with iText, separately from the PdfPig
    /// pass that builds the visual diff. Reusing one parse for both would mean
    /// re-deriving the exact tokenization Reader does (digit stripping, hyphen
    /// splitting, length bounds) in a second place, and any drift there would move
    /// the displayed number away from what your current API returns. Re-parsing is
    /// slightly slower but keeps the two responsibilities honestly separate.
    /// </summary>
    public static class SimilarityAlgorithm
    {
        /// <summary>Algorithm selector passed straight through to distanceMeasure. 2 = Manhattan, 1 = cosine.</summary>
        public const int AlgorithmId = 2;

        public const int RemoveTopCount = 10;
        public const int RemoveTopMinLength = 15;

        /// <returns>The "difference" percentage, matching what your existing API returns.</returns>
        public static double ComputeDifference(string leftPdfPath, string rightPdfPath)
        {
            var reader = new Reader();

            var first = reader.uniqueWordsForApi(leftPdfPath);
            var second = reader.uniqueWordsForApi(rightPdfPath);

            first = Reader.removeTopN(first, RemoveTopCount, RemoveTopMinLength);
            second = Reader.removeTopN(second, RemoveTopCount, RemoveTopMinLength);

            if (first.Count == 0 || second.Count == 0) return 0;

            var distance = LevenshteinDistance.distanceMeasure(first, second, AlgorithmId);
            distance = (1 - distance) * 100;

            if (double.IsNaN(distance) || double.IsInfinity(distance)) return 0;
            if (distance < 0) return 0;
            if (distance > 100) return 100;

            return distance;
        }
    }
}
