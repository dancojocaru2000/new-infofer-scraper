using System.Collections.Generic;
using System.Diagnostics;

namespace InfoferScraper {
	public static partial class Utils {
		[DebuggerStepThrough]
		public static void Deconstruct<T>(this IEnumerable<T> enumerable, out T? first, out IEnumerable<T> rest) {
			var enumerator = enumerable.GetEnumerator();
			first = enumerator.MoveNext() ? enumerator.Current : default;
			rest = enumerator.AsEnumerable();
		}

		[DebuggerStepThrough]
		private static IEnumerable<T> AsEnumerable<T>(this IEnumerator<T> enumerator) {
			while (enumerator.MoveNext()) yield return enumerator.Current;
		}
	}
}
