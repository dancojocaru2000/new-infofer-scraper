from gevent.pywsgi import WSGIServer
from server import app

def main():
	port = 5000

	import os
	try:
		port = int(os.environ['PORT'])
	except:
		pass

	print(f'Starting server on port {port}')
	http_server = WSGIServer(('', port), app)
	http_server.serve_forever()

if __name__ == '__main__':
	main()
