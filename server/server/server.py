print(f'Server {__name__=}')

import datetime

from flask import Flask, jsonify, url_for
from jsonschema import validate

from .cache import CachedData
from .scraper.schemas import TRAIN_INFO_SCHEMA
from .utils import get_hostname

app = Flask(__name__)

from .v2 import v2
app.register_blueprint(v2.bp)

@app.route('/')
def root():
	return 'Test'

@app.route('/train/.schema.json')
def get_train_info_schema():
	return jsonify(TRAIN_INFO_SCHEMA['v1'])

train_data_cache = {}

@app.route('/train/<int:train_no>')
def get_train_info(train_no: int):
	def get_data():
		from .scraper.scraper import scrape_train
		use_yesterday = False
		result = scrape_train(train_no, use_yesterday=use_yesterday)

		from . import db
		db.on_train_data(result)

		# Convert to v1
		# datetime ISO string to hh:mm
		for i in range(len(result['stations'])):
			if result['stations'][i]['arrival']:
				date = datetime.datetime.fromisoformat(result['stations'][i]['arrival']['scheduleTime'])
				result['stations'][i]['arrival']['scheduleTime'] = f'{date.hour}:{date.minute:02}'
			if result['stations'][i]['departure']:
				date = datetime.datetime.fromisoformat(result['stations'][i]['departure']['scheduleTime'])
				result['stations'][i]['departure']['scheduleTime'] = f'{date.hour}:{date.minute:02}'

		return result
	if train_no not in train_data_cache:
		train_data_cache[train_no] = CachedData(get_data, validity=1000 * 30)
	data, fetch_time = train_data_cache[train_no]()
	data['$schema'] = get_hostname() + url_for('.get_train_info_schema')
	validate(data, schema=TRAIN_INFO_SCHEMA['v1'])
	resp = jsonify(data)
	resp.headers['X-Last-Fetched'] = fetch_time.isoformat()
	return resp

@app.route('/trains')
def get_trains():
	return jsonify(list(train_data_cache.keys()))

if __name__ == '__main__':
	print('Starting debug server on port 5001')
	app.run(port=5000)
