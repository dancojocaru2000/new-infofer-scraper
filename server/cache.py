from datetime import date, datetime, timedelta

_NO_DEFAULT = object()

class CachedData:
	def __init__(self, getter, initial_data=_NO_DEFAULT, validity=1000):
		self.getter = getter
		self.data = initial_data
		self.last_refresh_date = datetime.now()
		self.validity = timedelta(milliseconds=validity)
		if initial_data == _NO_DEFAULT:
			self.last_refresh_date -= self.validity

	def __call__(self, *args, **kwds):
		if self.last_refresh_date + self.validity < datetime.now():
			self.data = self.getter()
			self.last_refresh_date = datetime.now()
		return self.data, self.last_refresh_date
