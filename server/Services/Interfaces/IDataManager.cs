using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InfoferScraper.Models.Train;
using InfoferScraper.Models.Station;
using scraper.Models.Itinerary;

namespace Server.Services.Interfaces;

public interface IDataManager {
	public Task<IStationScrapeResult?> FetchStation(string stationName, DateTimeOffset date);
	public Task<ITrainScrapeResult?> FetchTrain(string trainNumber, DateTimeOffset date);
	public Task<IReadOnlyList<IItinerary>?> FetchItineraries(string from, string to, DateTimeOffset? date = null);
}
