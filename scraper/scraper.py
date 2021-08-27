#! /usr/bin/env python3
from .scrape_train import scrape as scrape_train
from .scrape_station import scrape as scrape_station

def main():
	train_no = 1538
	print(f'Testing package with train number {train_no}')
	from pprint import pprint
	pprint(scrape_train(train_no))

if __name__ == '__main__':
	main()
