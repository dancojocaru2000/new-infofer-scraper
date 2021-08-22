from scraper import scrape

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

def main():
	train_no = int(input('Train number: '))
	use_yesterday = input('Train departed yesterday? [y/N] ')
	data = scrape(train_no, use_yesterday=check_yes_no(use_yesterday, default=False))
	print(f'Train {train_no}\t{data["route"]["from"]}\t{data["route"]["to"]}')
	print()
	if 'status' in data and data['status']:
		delay = data['status']['delay']
		if delay == 0:
			delay = 'on time'
		else:
			delay = f'{delay} min'
		state = data['status']['state']
		station = data['status']['station']
		print(f'Status: {delay}\t{state}\t{station}')
		print()
	for station in data['stations']:
		if 'arrival' in station and station['arrival']:
			print(station['arrival']['scheduleTime'], end='\t')
		else:
			print(end='\t')
		print(station['name'], end='\t')
		if 'departure' in station and station['departure']:
			print(station['departure']['scheduleTime'], end='\t')
		else:
			print(end='\t')
		print()

if __name__ == '__main__':
	main()