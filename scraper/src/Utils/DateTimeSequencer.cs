using System;

namespace InfoferScraper {
	public static partial class Utils {
		public class DateTimeSequencer {
			private DateTime _current;

			public DateTimeSequencer(int year, int month, int day) {
				_current = new DateTime(year, month, day);
				_current = _current.AddSeconds(-1);
			}

			public DateTimeSequencer(DateTime startingDateTime) {
				_current = startingDateTime.AddSeconds(-1);
			}

			public DateTime Next(int hour, int minute = 0, int second = 0) {
				DateTime potentialNewDate = new(_current.Year, _current.Month, _current.Day, hour, minute, second);
				if (_current >= potentialNewDate) {
					_current = potentialNewDate.AddDays(1);
					potentialNewDate = new(_current.Year, _current.Month, _current.Day, hour, minute, second);
				}
				_current = potentialNewDate;
				return _current;
			}
		}
	}
}
