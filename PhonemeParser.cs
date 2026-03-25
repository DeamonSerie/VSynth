using System;
using System.Collections.Generic;
using System.Linq;

namespace VSynthApp
{
    public interface IPhonemeParser
    {
        IReadOnlyList<char> ParseToLetters(string text);
    }

    public class SimplePhonemeParser : IPhonemeParser
    {
        private static readonly Dictionary<string, string> DigraphFallback = new()
        {
            ["TH"] = "TH",
            ["SH"] = "SH",
            ["CH"] = "CH",
            ["PH"] = "F",
            ["CK"] = "K",
            ["QU"] = "KW"
        };

        public IReadOnlyList<char> ParseToLetters(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<char>();

            string normalized = new string(text.ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z').ToArray());
            if (string.IsNullOrEmpty(normalized)) return Array.Empty<char>();

            var output = new List<char>();
            int i = 0;
            while (i < normalized.Length)
            {
                if (i < normalized.Length - 1)
                {
                    string pair = normalized.Substring(i, 2);
                    if (DigraphFallback.TryGetValue(pair, out var mapped))
                    {
                        foreach (char c in mapped)
                        {
                            if (c is >= 'A' and <= 'Z') output.Add(c);
                        }
                        i += 2;
                        continue;
                    }
                }

                char letter = normalized[i];
                output.Add(letter);
                i++;
            }

            return output;
        }
    }
}
