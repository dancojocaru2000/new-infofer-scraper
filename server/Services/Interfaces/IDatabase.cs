using System.Collections.Generic;
using System.Threading.Tasks;
using InfoferScraper.Models.Train;
using InfoferScraper.Models.Station;

namespace Server.Services.Interfaces;

public interface IDatabase {
	public IReadOnlyList<IStationRecord> Stations { get; }
	public IReadOnlyList<ITrainRecord> Trains { get; }

	public Task<string> FoundTrain(string rank, string number, string company);
	public Task FoundStation(string name);
	public Task FoundTrainAtStation(string stationName, string trainName);
	public Task OnTrainData(ITrainScrapeResult trainData);
	public Task OnStationData(IStationScrapeResult stationData);
}

public interface IStationRecord {
	public string Name { get; }
	public IReadOnlyList<string> StoppedAtBy { get; }
}

public interface ITrainRecord {
	public string Rank { get; }
	public string Number { get; }
	public string Company { get; }
}
