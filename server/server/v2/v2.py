import json
from flask import Blueprint, jsonify, request
from flask.helpers import url_for
from jsonschema import validate

from .. import db
from ..cache import CachedData
from ..utils import check_yes_no, get_hostname
from ..flask_utils import filtered_data
from ..scraper.utils import ro_letters_to_en
from ..scraper.schemas import STATION_SCHEMA, TRAIN_INFO_SCHEMA

bp = Blueprint('v2', __name__, url_prefix='/v2')

@bp.get('/trains')
def get_known_trains():
	@filtered_data
	def get_data():
		return db.trains

	result = get_data()

	return jsonify(result)

@bp.get('/stations')
def get_known_stations():
	@filtered_data
	def get_data():
		return db.stations

	result = get_data()

	return jsonify(result)

train_data_cache = {}

@bp.route('/train/.schema.json')
def get_train_info_schema():
	return jsonify(TRAIN_INFO_SCHEMA['v2'])

@bp.route('/train/<int:train_no>')
def get_train_info(train_no: int):
	use_yesterday = check_yes_no(request.args.get('use_yesterday', ''), default=False)
	@filtered_data
	def get_data():
		from ..scraper.scraper import scrape_train
		result = scrape_train(train_no, use_yesterday=use_yesterday)
		db.on_train_data(result)
		return result
	if (train_no, use_yesterday) not in train_data_cache:
		train_data_cache[(train_no, use_yesterday)] = CachedData(get_data, validity=1000 * 30)
	data, fetch_time = train_data_cache[(train_no, use_yesterday)]()
	data['$schema'] = get_hostname() + url_for('.get_train_info_schema')
	validate(data, schema=TRAIN_INFO_SCHEMA['v2'])
	resp = jsonify(data)
	resp.headers['X-Last-Fetched'] = fetch_time.isoformat()
	return resp

station_cache = {}

@bp.route('/station/.schema.json')
def get_station_schema():
	return jsonify(STATION_SCHEMA['v2'])

@bp.route('/station/<station_name>')
def get_station(station_name: str):
	station_name = ro_letters_to_en(station_name.lower().replace(' ', '-'))

	def get_data():
		from ..scraper.scraper import scrape_station
		result = scrape_station(station_name)
		db.on_station(result)
		return result
	if station_name not in train_data_cache:
		station_cache[station_name] = CachedData(get_data, validity=1000 * 30)
	data, fetch_time = station_cache[station_name]()
	data['$schema'] = get_hostname() + url_for('.get_station_schema')
	validate(data, schema=STATION_SCHEMA['v2'])

	@filtered_data
	def filter(data):
		return data

	resp = jsonify(filter(data))
	resp.headers['X-Last-Fetched'] = fetch_time.isoformat()
	return resp
	
