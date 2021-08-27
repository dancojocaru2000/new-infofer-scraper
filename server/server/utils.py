def take_while(predicate, input):
	for element in input:
		if not predicate(element):
			break
		yield element

_NO_DEFAULT = object()

def check_yes_no(input: str, default=_NO_DEFAULT, considered_yes=None) -> bool:
	input = str(input).strip().lower()
	if not input:
		if default == _NO_DEFAULT:
			raise Exception('Empty input with no default')
		return default
	if not considered_yes:
		considered_yes = ['y', 'yes', 't', 'true', '1']
	return input in considered_yes

def get_hostname():
	import os
	import platform
	return os.getenv('HOSTNAME', os.getenv('COMPUTERNAME', platform.node()))

def filter_result(data, properties=None, filters=None):
	is_array = not hasattr(data, 'get')
	result = data if is_array else [data]

	if filters:
		# Todo: implement filters
		pass
		# def f(lst, filters):
		# 	def condition(item):
				
		# 	return list(filter(condition, lst))
		# result = f(result, filters)

	if properties:
		for i in range(len(result)):
			result[i] = {p:result[i].get(p, None) for p in properties}

	return result if is_array else result[0]
