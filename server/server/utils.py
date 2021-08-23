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

