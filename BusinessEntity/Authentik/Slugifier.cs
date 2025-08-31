using System.Text;
using System.Text.RegularExpressions;

namespace BusinessEntity.Authentik
{
    /// <summary>
    /// Utility to convert arbitrary application names into URL-friendly slugs.
    /// Preserves only [a-z0-9-], compresses repeated dashes, trims to 50 chars.
    /// </summary>
    internal static class Slugifier
    {
        /// <summary>
        /// Convert the provided string to a URL-friendly slug.
        /// </summary>
        public static string ToSlug(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            var lower = input.Trim().ToLowerInvariant();
            // Replace spaces with hyphens
            lower = Regex.Replace(lower, @"\s+", "-");
            // Keep only a-z, 0-9 and '-'
            var sb = new StringBuilder(lower.Length);
            foreach (var ch in lower)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-')
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append('-');
                }
            }
            // Compress multiple '-'
            var slug = Regex.Replace(sb.ToString(), "-+", "-");
            // Trim leading/trailing '-'
            slug = slug.Trim('-');
            if (slug.Length > 50)
                slug = slug.Substring(0, 50);
            return slug;
        }
    }
}
