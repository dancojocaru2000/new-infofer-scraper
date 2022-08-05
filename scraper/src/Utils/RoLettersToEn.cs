using System.Collections.Generic;
using System.Linq;

namespace InfoferScraper;

public static partial class Utils {
	private static readonly Dictionary<char, char> RoToEn = new() {
		{ 'ă', 'a' },
		{ 'Ă', 'A' },
		{ 'â', 'a' },
		{ 'Â', 'A' },
		{ 'î', 'i' },
		{ 'Î', 'I' },
		{ 'ș', 's' },
		{ 'Ș', 'S' },
		{ 'ț', 't' },
		{ 'Ț', 'T' },
	};

	public static string RoLettersToEn(this string str) {
		return string.Concat(str.Select(letter => RoToEn.GetValueOrDefault(letter, letter)));
	}
}
