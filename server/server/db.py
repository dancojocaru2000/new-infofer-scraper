# Globals
stations = []
trains = []
db_data = {
	'version': 2,
}

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
from contextlib import contextmanager

from .utils import take_while

DB_DIR = os.environ.get('DB_DIR', '') or './db'
if not path.exists(DB_DIR):
	os.mkdir(DB_DIR)

DB_FILE = path.join(DB_DIR, 'db.json')

STATIONS_FILE = path.join(DB_DIR, 'stations.json')

TRAINS_FILE = path.join(DB_DIR, 'trains.json')

def migration():
	global db_data
	global trains
	global stations
	if not path.exists(DB_FILE):
		print('[Migration] Migrating DB version 1 -> 2')
		if path.exists(STATIONS_FILE):
			with open(STATIONS_FILE) as f:
				stations = json.load(f)
			for i in range(len(stations)):
				stations[i]['stoppedAtBy'] = [str(num) for num in stations[i]['stoppedAtBy']]
			with open(STATIONS_FILE, 'w') as f:
				json.dump(stations, f)
		if path.exists(TRAINS_FILE):
			with open(TRAINS_FILE) as f:
				trains = json.load(f)
			for i in range(len(trains)):
				trains[i]['number'] = trains[i]['numberString']
				del trains[i]['numberString']
			with open(TRAINS_FILE, 'w') as f:
				json.dump(trains, f)
		db_data = {
			'version': 2,
		}
		with open(DB_FILE, 'w') as f:
			json.dump(db_data, f)
		migration()
	else:
		with open(DB_FILE) as f:
			db_data = json.load(f)
		if db_data['version'] == 2:
			print('[Migration] DB Version: 2, noop')

migration()

if path.exists(DB_FILE):
	with open(DB_FILE) as f:
		db_data = json.load(f)
else:
	with open(DB_FILE, 'w') as f:
		json.dump(db_data, f)

if path.exists(STATIONS_FILE):
	with open(STATIONS_FILE) as f:
		stations = json.load(f)

if path.exists(TRAINS_FILE):
	with open(TRAINS_FILE) as f:
		trains = json.load(f)

_should_commit_on_every_change = True

@contextmanager
def db_transaction():
	global _should_commit_on_every_change
	_should_commit_on_every_change = False
	yield
	with open(DB_FILE, 'w') as f:
		json.dump(db_data, f)
	with open(STATIONS_FILE, 'w') as f:
		stations.sort(key=lambda s: len(s['stoppedAtBy']), reverse=True)
		json.dump(stations, f)
	with open(TRAINS_FILE, 'w') as f:
		json.dump(trains, f)
	_should_commit_on_every_change = True

def found_train(rank: str, number: str, company: str) -> int:
	number = ''.join(take_while(lambda s: str(s).isnumeric(), number))
	try:
		next(filter(lambda tr: tr['number'] == number, trains))
	except StopIteration:
		trains.append({
			'number': number,
			'company': company,
			'rank': rank,
		})
		if _should_commit_on_every_change:
			with open(TRAINS_FILE, 'w') as f:
				json.dump(trains, f)
	return number

def found_station(name: str):
	try:
		next(filter(lambda s: s['name'] == name, stations))
	except StopIteration:
		stations.append({
			'name': name,
			'stoppedAtBy': [],
		})
		if _should_commit_on_every_change:
			stations.sort(key=lambda s: len(s['stoppedAtBy']), reverse=True)
			with open(STATIONS_FILE, 'w') as f:
				json.dump(stations, f)

def found_train_at_station(station_name: str, train_number: str):
	train_number = ''.join(take_while(lambda s: str(s).isnumeric(), train_number))
	found_station(station_name)
	for i in range(len(stations)):
		if stations[i]['name'] == station_name:
			if train_number not in stations[i]['stoppedAtBy']:
				stations[i]['stoppedAtBy'].append(train_number)
			break
	if _should_commit_on_every_change:
		stations.sort(key=lambda s: len(s['stoppedAtBy']), reverse=True)
		with open(STATIONS_FILE, 'w') as f:
			json.dump(stations, f)

def on_train_data(train_data: dict):
	with db_transaction():
		train_no = found_train(train_data['rank'], train_data['number'], train_data['operator'])
		for station in train_data['stations']:
			found_train_at_station(station['name'], train_no)

def on_train_lookup_failure(train_no: str):
	pass

def on_station(station_data: dict):
	station_name = station_data['stationName']

	def process_train(train_data: dict):
		train_number = train_data['train']['number']
		train_number = found_train(train_data['train']['rank'], train_number, train_data['train']['operator'])
		found_train_at_station(station_name, train_number)
		if 'route' in train_data['train'] and train_data['train']['route']:
			for station in train_data['train']['route']:
				found_train_at_station(station, train_number)
	
	with db_transaction():
		for train in station_data['arrivals']:
			process_train(train)
		for train in station_data['departures']:
			process_train(train)
