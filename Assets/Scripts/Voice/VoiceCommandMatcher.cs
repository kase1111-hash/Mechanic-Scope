using System.Collections.Generic;
using System.Text;

namespace MechanicScope.Voice
{
    /// <summary>
    /// Maps recognized speech to registered command phrases.
    ///
    /// Matching is on whole words after normalization, so "unlock" never triggers the "lock"
    /// command and "stopwatch" never triggers "stop". When several phrases occur in one utterance,
    /// the one with the most words wins ("next step" beats "next"), then the longest.
    /// </summary>
    public class VoiceCommandMatcher<T> where T : class
    {
        private readonly Dictionary<string, T> phrases = new Dictionary<string, T>();

        public int Count => phrases.Count;

        /// <summary>Registers a phrase. A later registration of the same phrase replaces the earlier one.</summary>
        public void Add(string phrase, T command)
        {
            string normalized = Normalize(phrase);
            if (normalized.Length > 0)
            {
                phrases[normalized] = command;
            }
        }

        /// <summary>
        /// Returns the command whose phrase best matches the utterance, or null. The matched
        /// (normalized) phrase is returned through <paramref name="matchedPhrase"/>.
        /// </summary>
        public T Match(string utterance, out string matchedPhrase)
        {
            matchedPhrase = null;
            T best = null;
            int bestWords = 0;

            string padded = " " + Normalize(utterance) + " ";
            if (padded.Length <= 2) return null;

            foreach (KeyValuePair<string, T> entry in phrases)
            {
                if (!padded.Contains(" " + entry.Key + " ")) continue;

                int words = CountWords(entry.Key);
                if (best == null || words > bestWords ||
                    (words == bestWords && entry.Key.Length > matchedPhrase.Length))
                {
                    best = entry.Value;
                    bestWords = words;
                    matchedPhrase = entry.Key;
                }
            }

            return best;
        }

        /// <summary>
        /// Lower-cases, drops apostrophes ("what's" and "whats" compare equal), turns every other
        /// non-alphanumeric character into a word break, and collapses whitespace. Platform
        /// recognizers differ in capitalization and punctuation ("Next step." vs "next step").
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder(text.Length);
            bool pendingSpace = false;

            foreach (char raw in text)
            {
                if (raw == '\'' || raw == '’') continue;

                if (char.IsLetterOrDigit(raw))
                {
                    if (pendingSpace && sb.Length > 0) sb.Append(' ');
                    pendingSpace = false;
                    sb.Append(char.ToLowerInvariant(raw));
                }
                else
                {
                    pendingSpace = true;
                }
            }

            return sb.ToString();
        }

        private static int CountWords(string normalized)
        {
            int count = 1;
            foreach (char c in normalized)
            {
                if (c == ' ') count++;
            }
            return count;
        }
    }
}
