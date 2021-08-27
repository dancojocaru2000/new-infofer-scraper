import re

from datetime import datetime, timedelta
from urllib.parse import urlencode, quote

# From: https://en.wikipedia.org/wiki/Whitespace_character#Unicode
ASCII_WHITESPACE = [
	'\u0009', # HT; Character Tabulation 
	'\u000a', # LF
	'\u000b', # VT; Line Tabulation
	'\u000c', # FF; Form Feed
	'\u000d', # CR
	'\u0020', # Space
]

WHITESPACE = ASCII_WHITESPACE + [
	'\u0085', # NEL; Next Line
	'\u00a0', # No-break Space; &nbsp;
	'\u1680', # Ogham Space Mark
	'\u2000', # En Quad
	'\u2001', # Em Quad
	'\u2002', # En Space
	'\u2003', # Em Space
	'\u2004', # Three-per-em Space
	'\u2005', # Four-per-em Space
	'\u2006', # Six-per-em Space
	'\u2007', # Figure Space
	'\u2008', # Punctuation Space
	'\u2009', # Thin Space
	'\u200A', # Hair Space
	'\u2028', # Line Separator
	'\u2029', # Paragraph Separator
	'\u202f', # Narrow No-break Space
	'\u205d', # Meduam Mathematical Space
	'\u3000', # Ideographic Space
]

WHITESPACE_REGEX = re.compile(rf'[{"".join(WHITESPACE)}]+', flags=re.MULTILINE)

class DateTimeSequencer:
	def __init__(self, year: int, month: int, day: int) -> None:
		self.current = datetime(year, month, day, 0, 0, 0)
		self.current -= timedelta(seconds=1)

	def __call__(self, hour: int, minute: int = 0, second: int = 0) -> datetime:
		potential_new_date = datetime(self.current.year, self.current.month, self.current.day, hour, minute, second)
		if (self.current > potential_new_date):
			potential_new_date += timedelta(days=1)
		self.current = potential_new_date
		return self.current

def collapse_space(string: str) -> str:
	return WHITESPACE_REGEX.sub(
		' ', 
		string, 
	).strip()

def build_url(base: str, /, query: dict = {}, **kwargs):
	result = base.format(**{ k: quote(str(v)) for k, v in kwargs.items() })
	if query:
		result += '?'
		result += urlencode(query)
	return result

RO_TO_EN = {
	'ă': 'a',
	'Ă': 'A',
	'â': 'a',
	'Â': 'A',
	'î': 'i',
	'Î': 'I',
	'ș': 's',
	'Ș': 'S',
	'ț': 't',
	'Ț': 'T',
}

def ro_letters_to_en(string: str) -> str:
	return ''.join((RO_TO_EN.get(letter, letter) for letter in string))
