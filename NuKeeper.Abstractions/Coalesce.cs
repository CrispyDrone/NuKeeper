using System.Collections.Generic;
using System.Linq;

namespace NuKeeper.Abstractions
{
    public static class Coalesce
    {
        public static string FirstValueOrDefault(params string[] values)
        {
            return values.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }

        public static T FirstValueOrDefault<T>(params T?[] values) where T : struct
        {
            return values.FirstOrDefault(i => i.HasValue) ?? default;
        }

        public static IReadOnlyCollection<T> FirstPopulatedListOrDefault<T>(params List<T>[] lists)
        {
            return lists.FirstOrDefault(HasElements);
        }

        public static IEnumerable<T> FirstPopulatedOrDefault<T>(params IEnumerable<T>[] enumerable)
        {
            return enumerable.FirstOrDefault(c => c?.Any() ?? false);
        }

        private static bool HasElements<T>(ICollection<T> collection)
        {
            if (collection == null) return false;

            return collection.Count > 0;
        }
    }
}
