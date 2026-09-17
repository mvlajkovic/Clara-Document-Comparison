using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Clara.DocumentComparison
{
    public class Reader
    {
        
        public string ReadPDF(string pdfLocation)
        {
            var pdfDocument = new PdfDocument(new PdfReader(pdfLocation));
            var strategy = new LocationTextExtractionStrategy();
            StringBuilder processed = new StringBuilder();
            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); ++i)
            {
                var page = pdfDocument.GetPage(i);
                string text = PdfTextExtractor.GetTextFromPage(page, strategy);
                processed.Append(text);
            }
            return processed.ToString();
        }

        //MRKELA OVDE
        public Dictionary<string,int> uniqueWords(string pdfLocation)
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            HashSet<string> hash = new HashSet<string>();
            var pdfDocument = new PdfDocument(new PdfReader(pdfLocation));
            var strategy = new SimpleTextExtractionStrategy();
            StringBuilder processed = new StringBuilder();
            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); ++i)
            {
                var page = pdfDocument.GetPage(i);
               
                string text = PdfTextExtractor.GetTextFromPage(page, strategy);

                // Console.WriteLine(text);

                //uklanja simbole nepotrebne i pretvara u lower case
                text = Regex.Replace(text, @"(\s+|@|&|'|\(|\)|<|>|#|,|\.|;|\?|[0-9]|\-|\?|:|'|"")", " ").ToLower();

                foreach (string n in text.Split(' '))
                 {
                    if(n.Length > 1 && n.Length < 50)
                    {
                       
                        if (!hash.Contains(n))
                        {
                            hash.Add(n);
                            dict.Add(n, 1);
                        }
                        else
                        {
                            dict[n]++;
                        }
                    }
                }
                //Console.WriteLine(dict.Count);

            }
            return dict;
        }

        //ovo ce biti da poredi fajl po fajl metoda al nisam dovrsio
        public static List<string> getAllFilesInFolder(string path)
        {
            List<string> paths = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(path, "*.pdf"))
                {
                    paths.Add(f);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }

            return paths;
        }

        public static Dictionary<String, int> removeTopN(Dictionary<string, int> dict, int n, int size)
        {
            var ordered = dict.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            List<string> toRemove = new List<string>();
            int i = 0;
            foreach (var kvp in ordered)
            {

                if (i == n)
                {
                    break;
                }
                i++;

                if (kvp.Key.Length < size)
                {
                    toRemove.Add(kvp.Key);
                }


            }
            foreach (var s in toRemove)
            {
                ordered.Remove(s);
            }

            return ordered;
        }

        public Dictionary<string, int> uniqueWordsForApi(string pdfLocation)
        {
            Dictionary<string, int> dict = new Dictionary<string, int>();
            HashSet<string> hash = new HashSet<string>();

            // Use 'using' to dispose PdfReader and PdfDocument properly
            using (var pdfReader = new PdfReader(pdfLocation))
            using (var pdfDocument = new PdfDocument(pdfReader))
            {
                var strategy = new SimpleTextExtractionStrategy();

                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); ++i)
                {
                    var page = pdfDocument.GetPage(i);
                    string text = PdfTextExtractor.GetTextFromPage(page, strategy);

                    // Clean and normalize text
                    text = Regex.Replace(text, @"(\s+|@|&|'|\(|\)|<|>|#|,|\.|;|\?|[0-9]|\-|\?|:|'|"")", " ").ToLower();

                    foreach (string n in text.Split(' '))
                    {
                        if (n.Length > 1 && n.Length < 50)
                        {
                            if (!hash.Contains(n))
                            {
                                hash.Add(n);
                                dict.Add(n, 1);
                            }
                            else
                            {
                                dict[n]++;
                            }
                        }
                    }
                }
            }

            return dict;
        }
    }
}
