using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clara.DocumentComparison
{
    public static class LevenshteinDistance
    {
        public static int Compute(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // Step 1
            if (n == 0)
            {
                return m;
            }

            if (m == 0)
            {
                return n;
            }

            // Step 2
            for (int i = 0; i <= n; d[i, 0] = i++)
            {
            }

            for (int j = 0; j <= m; d[0, j] = j++)
            {
            }

            // Step 3
            for (int i = 1; i <= n; i++)
            {
                //Step 4
                for (int j = 1; j <= m; j++)
                {
                    // Step 5
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    // Step 6
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            // Step 7
            return d[n, m];
        }

        public static IEnumerable<String> SplitInParts(this String s, Int32 partLength)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            if (partLength <= 0)
                throw new ArgumentException("Part length has to be positive.", nameof(partLength));

            for (var i = 0; i < s.Length; i += partLength)
                yield return s.Substring(i, Math.Min(partLength, s.Length - i));
        }


        //mrkela kako da implementiramo ovo s obzirom da sam ja koristio dictionary listu sa <string, int>
        public static double GetCosineSimilarity(List<double> V1, List<double> V2)
        {
            int N = 0;
            N = ((V2.Count < V1.Count) ? V2.Count : V1.Count);
            double dot = 0.0d;
            double mag1 = 0.0d;
            double mag2 = 0.0d;
            for (int n = 0; n < N; n++)
            {
                dot += V1[n] * V2[n];
                mag1 += Math.Pow(V1[n], 2);
                mag2 += Math.Pow(V2[n], 2);
            }

            return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
        }

        public static double getManhattanDistance(List<double> V1, List<double> V2)
        {
            int N = 0;
            N = ((V2.Count < V1.Count) ? V2.Count : V1.Count);
            double dot = 0.0d;
            double mag1 = 0.0d;
    
            for (int n = 0; n < N; n++)
            {
                dot += Math.Abs(V1[n] - V2[n]);
                // Console.WriteLine(Math.Abs(V1[n] - V2[n]));
                 mag1 += Math.Max(V1[n], V2[n]);
                //mag1 += V1[n] + V2[n];
                // Console.WriteLine(Math.Max(V1[n], V2[n]));

            }
           
            return 1 - dot / mag1;
        }


        public static double getEuclidanDistance(List<double> V1, List<double> V2)
        {
            int N = 0;
            N = ((V2.Count < V1.Count) ? V2.Count : V1.Count);
            double dot = 0.0d;
            double mag1 = 0.0d;

            for (int n = 0; n < N; n++)
            {
                dot += Math.Pow(Math.Abs(V1[n] - V2[n]),2);
                // Console.WriteLine(Math.Abs(V1[n] - V2[n]));
                mag1 += Math.Pow(Math.Max(V1[n], V2[n]), 2);
                // Console.WriteLine(Math.Max(V1[n], V2[n]));

            }

            return 1 - Math.Sqrt(dot) / Math.Sqrt(mag1);
        }

        //MRKELA OVDE
        public static double distanceMeasure(Dictionary<string, int> D1, Dictionary<string, int> D2,int algoritam)
        {
            List<double> V1 = new List<double>();
            List<double> V2 = new List<double>();

            foreach (string n in D1.Keys.Union(D2.Keys))
            {
                bool prva = D1.ContainsKey(n);
                bool druga = D2.ContainsKey(n);
                if (prva && druga)
                {
                    //Debug.WriteLine(n);
                    V1.Add(D1[n]);
                    V2.Add(D2[n]);
                }
                else if(prva)
                {
                   // Debug.WriteLine(n);
                    V1.Add(D1[n]);
                    V2.Add(0);
                }
                else if(druga)
                {
                   //Debug.WriteLine(n);
                    V1.Add(0);
                    V2.Add(D2[n]);
                }
                
            }

            
            if(algoritam == 1)
            {
                return GetCosineSimilarity(V1, V2);
            }
            if(algoritam == 2)
            {
                return getManhattanDistance(V1, V2);
            }
            return 0;
           
        }
    }
}
