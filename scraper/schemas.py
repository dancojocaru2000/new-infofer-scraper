from contextlib import ExitStack as _ExitStack

_es = _ExitStack()

def _load_file(name: str):
	import json
	from os.path import join, dirname
	dir = dirname(__file__)

	return json.load(_es.enter_context(open(join(dir, name))))

TRAIN_INFO_SCHEMA = {
	'v1': _load_file('scrape_train_schema.json'),
	'v2': _load_file('scrape_train_schema_v2.json'),
}
STATION_SCHEMA = {
	'v2': _load_file('scrape_station_schema_v2.json'),
}

_es.close()
