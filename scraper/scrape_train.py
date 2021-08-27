import re

from datetime import datetime, timedelta

import pytz
import requests
from bs4 import BeautifulSoup

from .utils import *

# region regex definitions

TRAIN_INFO_REGEX = re.compile(r'^([A-Z-]+) ([0-9]+) în ([0-9.]+)$')

OPERATOR_REGEX = re.compile(r'^Operat de (.+)$')

SL_REGEX = re.compile(r'^(?:Fără|([0-9]+) min) (întârziere|mai devreme) la (trecerea fără oprire prin|sosirea în|plecarea din) (.+)\.$')
SL_STATE_MAP = {
	't': 'passing',
	's': 'arrival',
	'p': 'departure',
}

RO_LETTERS = r'A-Za-zăâîșțĂÂÎȚȘ'

ROUTE_REGEX = re.compile(rf'^Parcurs tren ([{RO_LETTERS} ]+)[-–]([{RO_LETTERS} ]+)$')

KM_REGEX = re.compile(r'^km ([0-9]+)$')

PLATFORM_REGEX = re.compile(r'^linia (.+)$')

STOPPING_TIME_REGEX = re.compile(r'^([0-9]+) min oprire$')

STATION_DEPARR_STATUS_REGEX = re.compile(r'^(?:(la timp)|(?:((?:\+|-)[0-9]+) min \((?:(?:întârziere)|(?:mai devreme))\)))(\*?)$')

# endregion

def scrape(train_no: int, use_yesterday=False, date_override=None):
	# Start scrapping session
	s = requests.Session()

	date = datetime.today()
	if use_yesterday:
		date -= timedelta(days=1)
	if date_override:
		date = date_override

	r = s.get(build_url(
		'https://mersultrenurilor.infofer.ro/ro-RO/Tren/{train_no}', 
		train_no=train_no, 
		query=[
			('Date', date.strftime('%d.%m.%Y')),
		],
	))

	soup = BeautifulSoup(r.text, features='html.parser')
	sform = soup.find(id='form-search')
	result_data = { elem['name']: elem['value'] for elem in sform('input') }

	r = s.post('https://mersultrenurilor.infofer.ro/ro-RO/Trains/TrainsResult', data=result_data)
	soup = BeautifulSoup(r.text, features='html.parser')

	scraped = {}

	train_info_div, _, _, results_div, *_ = soup('div', recursive=False)

	train_info_div = train_info_div.div('div', recursive=False)[0]

	scraped['rank'], scraped['number'], scraped['date'] = TRAIN_INFO_REGEX.match(collapse_space(train_info_div.h2.text)).groups()
	date_d, date_m, date_y = (int(comp) for comp in scraped['date'].split('.'))
	date = datetime(date_y, date_m, date_d)

	scraped['operator'] = OPERATOR_REGEX.match(collapse_space(train_info_div.p.text)).groups()[0]

	results_div = results_div.div
	status_div = results_div('div', recursive=False)[0]
	route_text = collapse_space(status_div.h4.text)
	route_from, route_to = ROUTE_REGEX.match(route_text).groups()
	scraped['route'] = {
		'from': route_from,
		'to': route_to,
	}
	try:
		status_line_match = SL_REGEX.match(collapse_space(status_div.div.text))
		slm_delay, slm_late, slm_arrival, slm_station = status_line_match.groups()
		scraped['status'] = {
			'delay': (int(slm_delay) if slm_late == 'întârziere' else -int(slm_delay)) if slm_delay else 0,
			'station': slm_station,
			'state': SL_STATE_MAP[slm_arrival[0]],
		}
	except Exception:
		scraped['status'] = None

	stations = status_div.ul('li', recursive=False)
	scraped['stations'] = []
	dt_seq = DateTimeSequencer(date.year, date.month, date.day)
	tz = pytz.timezone('Europe/Bucharest')
	for station in stations:
		station_scraped = {}

		left, middle, right = station.div('div', recursive=False)
		station_scraped['name'] = collapse_space(middle.div.div('div', recursive=False)[0]('div', recursive=False)[0].text)
		station_scraped['km'] = collapse_space(middle.div.div('div', recursive=False)[0]('div', recursive=False)[1].text)
		station_scraped['km'] = int(KM_REGEX.match(station_scraped['km']).groups()[0])
		station_scraped['stoppingTime'] = collapse_space(middle.div.div('div', recursive=False)[0]('div', recursive=False)[2].text)
		if not station_scraped['stoppingTime']:
			station_scraped['stoppingTime'] = None
		else:
			station_scraped['stoppingTime'] = int(STOPPING_TIME_REGEX.match(station_scraped['stoppingTime']).groups()[0])
		station_scraped['platform'] = collapse_space(middle.div.div('div', recursive=False)[0]('div', recursive=False)[3].text)
		if not station_scraped['platform']:
			station_scraped['platform'] = None
		else:
			station_scraped['platform'] = PLATFORM_REGEX.match(station_scraped['platform']).groups()[0]

		def scrape_time(elem, setter):
			parts = elem.div.div('div', recursive=False)
			if parts:
				result = {}

				time, *_ = parts
				result['scheduleTime'] = collapse_space(time.text)
				st_hr, st_min = (int(comp) for comp in result['scheduleTime'].split(':'))
				result['scheduleTime'] = tz.localize(dt_seq(st_hr, st_min)).isoformat()
				if len(parts) >= 2:
					_, status, *_ = parts
					result['status'] = {}
					on_time, delay, approx = STATION_DEPARR_STATUS_REGEX.match(collapse_space(status.text)).groups()
					result['status']['delay'] = 0 if on_time else int(delay)
					result['status']['real'] = not approx
				else:
					result['status'] = None

				setter(result)
			else:
				setter(None)

		scrape_time(left, lambda value: station_scraped.update(arrival=value))
		scrape_time(right, lambda value: station_scraped.update(departure=value))

		scraped['stations'].append(station_scraped)

	return scraped
