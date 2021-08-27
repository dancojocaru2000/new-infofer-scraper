from flask import request as _f_request

from .utils import filter_result as _filter_result

def filtered_data(fn):
	def filterer(*args, **kwargs):
		filters = _f_request.args.get('filters', None)
		if filters:
			filters_raw = [f.split(':', 1) for f in filters.split(',')]
			filters = {'.': []}
			for key, value in filters_raw:
				def add_to(obj, key, value):
					if '.' in key:
						prop, key = key.split('.', 1)
						if prop not in filters:
							obj[prop] = {'.': []}
						add_to(obj[prop], key, value)
					else:
						obj['.'].append({key: value})
				add_to(filters, key, value)
		properties = _f_request.args.get('properties', None)
		if properties:
			properties = properties.split(',')

		data = fn(*args, **kwargs)

		return _filter_result(data, properties, filters)
		
	return filterer
