using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using InfoferScraper;
using InfoferScraper.Scrapers;

while (true) {
	Console.WriteLine("1. Scrape Train");
	Console.WriteLine("2. Scrape Station");
	Console.WriteLine("3. Scrape Itineraries");
	Console.WriteLine("0. Exit");

	var input = Console.ReadLine()?.Trim();
	switch (input) {
		case "1":
			await PrintTrain();
			break;
		case "2":
			await PrintStation();
			break;
		case "3":
			await ScrapeItineraries();
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
async Task ScrapeItineraries() {
	Console.Write("From station: ");
	var from = Console.ReadLine();
	Console.Write("To station: ");
	var to = Console.ReadLine();

	if (from == null || to == null) return;

	var data = await RouteScraper.Scrape(from, to);
    
	Console.WriteLine($"{data.Count} itineraries:");
	Console.WriteLine();

	void PrintArrDepLine(DateTimeOffset date, string station) {
		Console.WriteLine($"{date:HH:mm} {station}");
	}
    
	foreach (var itinerary in data) {
		foreach (var train in itinerary.Trains) {
			PrintArrDepLine(train.DepartureDate, train.From);
			Console.WriteLine($"  {train.TrainRank,-4} {train.TrainNumber,-5} ({train.Operator}), {train.Km,3} km via {string.Join(", ", train.IntermediateStops)}");
			PrintArrDepLine(train.ArrivalDate, train.To);
		}
        
		Console.WriteLine();
	}
}
