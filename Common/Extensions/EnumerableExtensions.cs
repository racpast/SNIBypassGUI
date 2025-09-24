using System.Collections.Generic;

namespace SNIBypassGUI.Common.Extensions
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns the source sequence or an empty sequence if the source is <c>null</c>.
        /// This prevents <see cref="System.NullReferenceException"/> when enumerating a potentially <c>null</c> collection.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
        /// <param name="source">The source sequence, which may be <c>null</c>.</param>
        /// <returns>The original <paramref name="source"/> sequence if it is not <c>null</c>; otherwise, an empty sequence.</returns>
        public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T> source) =>
            source ?? [];
    }
}
