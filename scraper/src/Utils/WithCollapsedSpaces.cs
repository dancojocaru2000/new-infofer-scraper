using System.Text.RegularExpressions;

namespace InfoferScraper {
	public static partial class Utils {
		private static readonly Regex WhitespaceRegex = new(@"(\s)\s*");

		public static string WithCollapsedSpaces(this string str, bool trim = true, string replaceWith = "$1") {
			var collapsed = WhitespaceRegex.Replace(str, replaceWith);
			return trim ? collapsed.Trim() : collapsed;
		}
	}
}
