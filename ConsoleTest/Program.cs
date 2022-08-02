using System;
using System.Text.Json;
using System.Threading.Tasks;
using InfoferScraper;
using InfoferScraper.Scrapers;

while (true) {
	Console.WriteLine("1. Scrape Train");
	Console.WriteLine("2. Scrape Station");
	Console.WriteLine("0. Exit");

	var input = Console.ReadLine()?.Trim();
	switch (input) {
		case "1":
			await PrintTrain();
			break;
		case "2":
			await PrintStation();
			break;
		case null:
		case "0":
			goto INPUT_LOOP_BREAK;
	}
	Console.WriteLine();
}
INPUT_LOOP_BREAK:;

async Task PrintTrain() {
	Console.Write("Train number: ");
	var trainNumber = Console.ReadLine()?.Trim();

	if (trainNumber == null) {
		return;
	}
	
	Console.WriteLine(
		JsonSerializer.Serialize(
			await TrainScraper.Scrape(trainNumber),
			new JsonSerializerOptions {
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				WriteIndented = true,
			}
		)
	);
}
async Task PrintStation() {
	Console.Write("Station name: ");
	var stationName = Console.ReadLine()?.Trim();

	if (stationName == null) {
		return;
	}
	
	Console.WriteLine(
		JsonSerializer.Serialize(
			await StationScraper.Scrape(stationName),
			new JsonSerializerOptions {
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				WriteIndented = true,
			}
		)
	);
}
