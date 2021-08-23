from flask import Blueprint, jsonify, request

from .. import db
from ..cache import CachedData
from ..utils import check_yes_no

bp = Blueprint('v2', __name__, url_prefix='/v2')

@bp.get('/trains')
def get_known_trains():
	return jsonify(db.trains)

@bp.get('/stations')
def get_known_stations():
	return jsonify(db.stations)

train_data_cache = {}

@bp.route('/train/<int:train_no>')
def get_train_info(train_no: int):
	use_yesterday = check_yes_no(request.args.get('use_yesterday', ''), default=False)
	def get_data():
		from ..scraper.scraper import scrape
		result = scrape(train_no, use_yesterday=use_yesterday)
		db.on_train_data(result)
		return result
	if train_no not in train_data_cache:
		train_data_cache[(train_no, use_yesterday)] = CachedData(get_data, validity=1000 * 30)
	data, fetch_time = train_data_cache[(train_no, use_yesterday)]()
	resp = jsonify(data)
	resp.headers['X-Last-Fetched'] = fetch_time.isoformat()
	return resp
