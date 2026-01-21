using System;
using System.Text.RegularExpressions;

namespace Database.Application
{
    public static class PathNormalizer
    {
        private static readonly Regex Invalid = new(
            @"(^/)|(^[A-Za-z]:)|(\.\.)|(\\)|(:)|(\s)",
            RegexOptions.Compiled);

        // Strict allow-list: letters/digits/._-/ and '/' only
        private static readonly Regex Allowed = new(
            @"^[A-Za-z0-9._\-/]+$",
            RegexOptions.Compiled);

        public static string? NormalizeArtPath(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var trimmed = raw.Trim();

            while (trimmed.Contains("//"))
                trimmed = trimmed.Replace("//", "/");

            if (Invalid.IsMatch(trimmed) || !Allowed.IsMatch(trimmed))
                throw new ArgumentException("Invalid art path.", nameof(raw));

            return trimmed;
        }
    }
}