using System;

namespace Folke.CsTsService
{
    public class StringHelpers
    {
        public static string ToCamelCase(string identifier)
        {
            return String.IsNullOrEmpty(identifier)
                ? identifier
                : identifier.Substring(0, 1).ToLowerInvariant() + identifier.Substring(1);
        }

        public static string ToPascalCase(string identifier)
        {
            return String.IsNullOrEmpty(identifier)
                ? identifier
                : identifier.Substring(0, 1).ToUpperInvariant() + identifier.Substring(1);
        }
    }
}
