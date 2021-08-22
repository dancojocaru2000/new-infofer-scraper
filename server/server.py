from flask import Flask, json, request, jsonify

from cache import CachedData

app = Flask(__name__)

@app.route('/')
def root():
	return 'Test'

train_data_cache = {}

@app.route('/train/<int:train_no>')
def get_train_info(train_no: int):
	def get_data():
		print(f'Cache miss for {train_no}')
		from scraper.scraper import scrape
		use_yesterday = False
		return scrape(train_no, use_yesterday=use_yesterday)
	if train_no not in train_data_cache:
		train_data_cache[train_no] = CachedData(get_data, validity=1000 * 30)
	data, fetch_time = train_data_cache[train_no]()
	resp = jsonify(data)
	resp.headers['X-Last-Fetched'] = fetch_time.isoformat()
	return resp

@app.route('/trains')
def get_trains():
	return jsonify(list(train_data_cache.keys()))

if __name__ == '__main__':
	print('Starting debug server on port 5001')
	app.run(port=5000)
