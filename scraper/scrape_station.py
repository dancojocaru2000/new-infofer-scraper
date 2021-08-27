import re

from datetime import datetime, timedelta

import pytz
import requests
from bs4 import BeautifulSoup

from .utils import *

# region regex definitions

RO_LETTERS = r'A-Za-zăâîșțĂÂÎȚȘ'

STATION_INFO_REGEX = re.compile(rf'^([{RO_LETTERS}. ]+) în ([0-9.]+)$')

STOPPING_TIME_REGEX = re.compile(r'^(necunoscută \(stație terminus\))|(?:([0-9]+) (min|sec) \((?:începând cu|până la) ([0-9]{1,2}:[0-9]{2})\))$')

# endregion

def scrape(station_name: str):
	station_name = ro_letters_to_en(station_name)
	# Start scrapping session
	s = requests.Session()

	r = s.get(build_url(
		'https://mersultrenurilor.infofer.ro/ro-RO/Statie/{station}', 
		station=station_name.replace(' ', '-'), 
	))

	soup = BeautifulSoup(r.text, features='html.parser')
	sform = soup.find(id='form-search')
	result_data = { elem['name']: elem['value'] for elem in sform('input') }

	r = s.post('https://mersultrenurilor.infofer.ro/ro-RO/Stations/StationsResult', data=result_data)
	soup = BeautifulSoup(r.text, features='html.parser')

	scraped = {}

	station_info_div, _, departures_div, arrivals_div, *_ = soup('div', recursive=False)

	scraped['stationName'], scraped['date'] = STATION_INFO_REGEX.match(collapse_space(station_info_div.h2.text)).groups()
	date_d, date_m, date_y = (int(comp) for comp in scraped['date'].split('.'))
	date = datetime(date_y, date_m, date_d)
	dt_seq = DateTimeSequencer(date.year, date.month, date.day)
	tz = pytz.timezone('Europe/Bucharest')

	def parse_arrdep_list(elem, end_station_field_name):
		if elem.div.ul is None:
			return None

		def parse_item(elem):
			result = {}

			try:
				data_div, status_div = elem('div', recursive=False)
			except ValueError:
				data_div, *_ = elem('div', recursive=False)
				status_div = None
			data_main_div, data_details_div = data_div('div', recursive=False)
			time_div, dest_div, train_div, *_ = data_main_div('div', recursive=False)
			operator_div, route_div, stopping_time_div = data_details_div.div('div', recursive=False)
			
			result['time'] = collapse_space(time_div.div.div('div', recursive=False)[1].text)
			st_hr, st_min = (int(comp) for comp in result['time'].split(':'))
			result['time'] = tz.localize(dt_seq(st_hr, st_min)).isoformat()

			unknown_st, st, minsec, st_opposite_time = STOPPING_TIME_REGEX.match(
				collapse_space(stopping_time_div.div('div', recursive=False)[1].text)
			).groups()
			if unknown_st:
				result['stoppingTime'] = None
			elif st:
				minutes = minsec == 'min'
				result['stoppingTime'] = int(st) * 60 if minutes else int(st)

			result['train'] = {}
			result['train']['rank'] = collapse_space(train_div.div.div('div', recursive=False)[1].span.text)
			result['train']['number'] = collapse_space(train_div.div.div('div', recursive=False)[1].a.text)
			result['train'][end_station_field_name] = collapse_space(dest_div.div.div('div', recursive=False)[1].text)
			result['train']['operator'] = collapse_space(operator_div.div('div', recursive=False)[1].text)
			result['train']['route'] = collapse_space(route_div.div('div', recursive=False)[1].text).split(' - ')

			return result

		return [parse_item(elem) for elem in elem.div.ul('li', recursive=False)]

	scraped['departures'] = parse_arrdep_list(departures_div, 'destination')
	scraped['arrivals'] = parse_arrdep_list(arrivals_div, 'origin')

	return scraped
