using System.Collections.Generic;
using System.Threading.Tasks;
using InfoferScraper.Models.Train;
using InfoferScraper.Models.Station;
using scraper.Models.Itinerary;
using Server.Models.Database;

namespace Server.Services.Interfaces;

public interface IDatabase {
	public IReadOnlyList<StationListing> Stations { get; }
	public IReadOnlyList<TrainListing> Trains { get; }

	public Task<string> FoundTrain(string rank, string number, string company);
	public Task FoundStation(string name);
	public Task FoundTrainAtStation(string stationName, string trainName);
	public Task OnTrainData(ITrainScrapeResult trainData);
	public Task OnStationData(IStationScrapeResult stationData);
	public Task OnItineraries(IReadOnlyList<IItinerary> itineraries);
}
