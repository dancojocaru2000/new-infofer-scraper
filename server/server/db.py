# Globals
stations = []
trains = []

# Examples
example_station = {
	'name': 'Gară',
	'stoppedAtBy': [123, 456]
}

example_train = {
	'rank': 'IR',
	'numberString': '74',
	'number': 74,
	'company': 'CFR Călători'
}

# Init

import json
import os
from os import path, stat
from .utils import take_while

DB_DIR = os.environ.get('DB_DIR', '') or './db'
if not path.exists(DB_DIR):
	os.mkdir(DB_DIR)

STATIONS_FILE = path.join(DB_DIR, 'stations.json')

if path.exists(STATIONS_FILE):
	with open(STATIONS_FILE) as f:
		stations = json.load(f)

TRAINS_FILE = path.join(DB_DIR, 'trains.json')

if path.exists(TRAINS_FILE):
	with open(TRAINS_FILE) as f:
		trains = json.load(f)

def found_train(rank: str, number: str, company: str) -> int:
	number_int = int(''.join(take_while(lambda s: str(s).isnumeric(), number)))
	try:
		next(filter(lambda tr: tr['number'] == number_int, trains))
	except StopIteration:
		trains.append({
			'number': number_int,
			'numberString': number,
			'company': company,
			'rank': rank,
		})
		with open(TRAINS_FILE, 'w') as f:
			json.dump(trains, f)
	return number_int

def found_station(name: str):
	try:
		next(filter(lambda s: s['name'] == name, stations))
	except StopIteration:
		stations.append({
			'name': name,
			'stoppedAtBy': [],
		})
		stations.sort(key=lambda s: len(s['stoppedAtBy']), reverse=True)
		with open(STATIONS_FILE, 'w') as f:
			json.dump(stations, f)

def found_train_at_station(station_name: str, train_number: int):
	found_station(station_name)
	for i in range(len(stations)):
		if stations[i]['name'] == station_name:
			if train_number not in stations[i]['stoppedAtBy']:
				stations[i]['stoppedAtBy'].append(train_number)
				stations.sort(key=lambda s: len(s['stoppedAtBy']), reverse=True)
				with open(STATIONS_FILE, 'w') as f:
					json.dump(stations, f)
			break

def on_train_data(train_data: dict):
	train_no = found_train(train_data['rank'], train_data['number'], train_data['operator'])
	for station in train_data['stations']:
		found_train_at_station(station['name'], train_no)

def on_train_lookup_failure(train_no: int):
	pass
