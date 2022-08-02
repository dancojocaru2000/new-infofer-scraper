using System;
using System.Threading.Tasks;
using InfoferScraper.Models.Train;
using InfoferScraper.Models.Station;

namespace Server.Services.Interfaces;

public interface IDataManager {
	public Task<IStationScrapeResult?> FetchStation(string stationName, DateTimeOffset date);
	public Task<ITrainScrapeResult?> FetchTrain(string trainNumber, DateTimeOffset date);
}
