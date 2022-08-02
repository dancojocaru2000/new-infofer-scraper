using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Server.Services.Implementations;

public class Database : Server.Services.Interfaces.IDatabase {
	private static readonly JsonSerializerOptions serializerOptions = new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private ILogger<Database> Logger { get; }

	private bool shouldCommitOnEveryChange = true;
	private bool dbDataDirty = false;
	private bool stationsDirty = false;
	private bool trainsDirty = false;

	public DbRecord DbData { get; private set; } = new(2);
	private List<StationRecord> stations = new();
	private List<TrainRecord> trains = new();

	public IReadOnlyList<Server.Services.Interfaces.IStationRecord> Stations => stations;
	public IReadOnlyList<Server.Services.Interfaces.ITrainRecord> Trains => trains;

	private static readonly string DbDir = Environment.GetEnvironmentVariable("DB_DIR") ?? Path.Join(Environment.CurrentDirectory, "db");
	private static readonly string DbFile = Path.Join(DbDir, "db.json");
	private static readonly string StationsFile = Path.Join(DbDir, "stations.json");
	private static readonly string TrainsFile = Path.Join(DbDir, "trains.json");

	public IDisposable MakeDbTransaction() {
		shouldCommitOnEveryChange = false;
		return new Server.Utils.ActionDisposable(() => {
			if (dbDataDirty) File.WriteAllText(DbFile, JsonSerializer.Serialize(DbData, serializerOptions));
			if (stationsDirty) {
				stations.Sort((s1, s2) => s2.StoppedAtBy.Count.CompareTo(s1.StoppedAtBy.Count));
				File.WriteAllText(StationsFile, JsonSerializer.Serialize(stations, serializerOptions));
			}
			if (trainsDirty) File.WriteAllText(TrainsFile, JsonSerializer.Serialize(trains, serializerOptions));
			dbDataDirty = stationsDirty = trainsDirty = false;
			shouldCommitOnEveryChange = true;
		});
	}

	public Database(ILogger<Database> logger) {
		Logger = logger;

		if (!Directory.Exists(DbDir)) {
			Logger.LogDebug("Creating directory: {DbDir}", DbDir);
			Directory.CreateDirectory(DbDir);
		}

		Migration();

		if (File.Exists(DbFile)) {
			DbData = JsonSerializer.Deserialize<DbRecord>(File.ReadAllText(DbFile), serializerOptions)!;
		}
		else {
			File.WriteAllText(DbFile, JsonSerializer.Serialize(DbData, serializerOptions));
		}

		if (File.Exists(StationsFile)) {
			stations = JsonSerializer.Deserialize<List<StationRecord>>(File.ReadAllText(StationsFile), serializerOptions)!;
		}

		if (File.Exists(TrainsFile)) {
			trains = JsonSerializer.Deserialize<List<TrainRecord>>(File.ReadAllText(TrainsFile), serializerOptions)!;
		}
	}

	private void Migration() {
		if (!File.Exists(DbFile)) {
// 			using var _ = Logger.BeginScope("Migrating DB version 1 -> 2");
			Logger.LogInformation("Migrating DB version 1 -> 2");
			if (File.Exists(StationsFile)) {
				Logger.LogDebug("Converting StationsFile");
				var oldStations = JsonNode.Parse(File.ReadAllText(StationsFile));
				if (oldStations != null) {
					Logger.LogDebug("Found {StationsCount} stations", oldStations.AsArray().Count);
					foreach (var station in oldStations.AsArray()) {
						if (station == null) continue;
						station["stoppedAtBy"] = new JsonArray(station["stoppedAtBy"]!.AsArray().Select(num => (JsonNode)(num!).ToString()!).ToArray());
					}
					stations = JsonSerializer.Deserialize<List<StationRecord>>(oldStations, serializerOptions)!;
				}
				Logger.LogDebug("Rewriting StationsFile");
				File.WriteAllText(StationsFile, JsonSerializer.Serialize(stations, serializerOptions));
			}
			if (File.Exists(TrainsFile)) {
				Logger.LogDebug("Converting TrainsFile");
				var oldTrains = JsonNode.Parse(File.ReadAllText(TrainsFile));
				if (oldTrains != null) {
					Logger.LogDebug("Found {TrainsCount} trains", oldTrains.AsArray().Count);
					foreach (var train in oldTrains.AsArray()) {
						if (train == null) continue;
						train["number"] = train["numberString"];
						train.AsObject().Remove("numberString");
					}
					trains = JsonSerializer.Deserialize<List<TrainRecord>>(oldTrains, serializerOptions)!;
				}
				Logger.LogDebug("Rewriting TrainsFile");
				File.WriteAllText(TrainsFile, JsonSerializer.Serialize(trains, serializerOptions));
			}
			DbData = new(2);
			File.WriteAllText(DbFile, JsonSerializer.Serialize(DbData, serializerOptions));
			Migration();
		}
		else {
			var oldDbData = JsonNode.Parse(File.ReadAllText(DbFile));
			if (((int?)oldDbData?["version"]) == 2) {
				Logger.LogInformation("DB Version: 2; noop");
			}
			else {
				throw new Exception("Unexpected Database version");
			}
		}
	}

	public async Task<string> FoundTrain(string rank, string number, string company) {
		number = string.Join("", number.TakeWhile(c => '0' <= c && c <= '9'));
		if (!trains.Where(train => train.Number == number).Any()) {
			Logger.LogDebug("Found train {Rank} {Number} from {Company}", rank, number, company);
			trains.Add(new(number, rank, company));
			if (shouldCommitOnEveryChange) {
				await File.WriteAllTextAsync(TrainsFile, JsonSerializer.Serialize(trains, serializerOptions));
			}
			else {
				trainsDirty = true;
			}
		}
		return number;
	}

	public async Task FoundStation(string name) {
		if (!stations.Where(station => station.Name == name).Any()) {
			Logger.LogDebug("Found station {StationName}", name);
			stations.Add(new(name, new()));
			if (shouldCommitOnEveryChange) {
				await File.WriteAllTextAsync(StationsFile, JsonSerializer.Serialize(stations, serializerOptions));
			}
			else {
				stationsDirty = true;
			}
		}
	}

	public async Task FoundTrainAtStation(string stationName, string trainNumber) {
		trainNumber = string.Join("", trainNumber.TakeWhile(c => '0' <= c && c <= '9'));
		await FoundStation(stationName);
		var dirty = false;
		for (var i = 0; i < stations.Count; i++) {
			if (stations[i].Name == stationName) {
				if (!stations[i].StoppedAtBy.Contains(trainNumber)) {
					Logger.LogDebug("Found train {TrainNumber} at station {StationName}", trainNumber, stationName);
					stations[i].ActualStoppedAtBy.Add(trainNumber);
					dirty = true;
				}
				break;
			}
		}
		if (dirty) {
			if (shouldCommitOnEveryChange) {
				stations.Sort((s1, s2) => s2.StoppedAtBy.Count.CompareTo(s1.StoppedAtBy.Count));
				await File.WriteAllTextAsync(StationsFile, JsonSerializer.Serialize(stations, serializerOptions));
			}
			else {
				stationsDirty = true;
			}
		}
	}

	public async Task OnTrainData(InfoferScraper.Models.Train.ITrainScrapeResult trainData) {
		using var _ = MakeDbTransaction();
		var trainNumber = await FoundTrain(trainData.Rank, trainData.Number, trainData.Operator);
		foreach (var group in trainData.Groups) {
			foreach (var station in group.Stations) {
				await FoundTrainAtStation(station.Name, trainNumber);
			}
		}
	}

	public async Task OnStationData(InfoferScraper.Models.Station.IStationScrapeResult stationData) {
		var stationName = stationData.StationName;

		async Task ProcessTrain(InfoferScraper.Models.Station.IStationArrDep train) {
			var trainNumber = train.Train.Number;
			trainNumber = await FoundTrain(train.Train.Rank, trainNumber, train.Train.Operator);
			await FoundTrainAtStation(stationName, trainNumber);
			if (train.Train.Route.Count != 0) {
				foreach (var station in train.Train.Route) {
					await FoundTrainAtStation(station, trainNumber);
				}
			}
		}

		using var _ = MakeDbTransaction();

		if (stationData.Arrivals != null) {
			foreach (var train in stationData.Arrivals) {
				await ProcessTrain(train);
			}
		}
		if (stationData.Departures != null) {
			foreach (var train in stationData.Departures) {
				await ProcessTrain(train);
			}
		}
	}
}

public record DbRecord(int Version);

public record StationRecord : Server.Services.Interfaces.IStationRecord {
	[JsonPropertyName("stoppedAtBy")]
	public List<string> ActualStoppedAtBy { get; init; }

	public string Name { get; init; }
	[JsonIgnore]
	public IReadOnlyList<string> StoppedAtBy => ActualStoppedAtBy;

	public StationRecord() {
		Name = "";
		ActualStoppedAtBy = new();
	}

	public StationRecord(string name, List<string> stoppedAtBy) {
		Name = name;
		ActualStoppedAtBy = stoppedAtBy;
	}
}

public record TrainRecord(string Number, string Rank, string Company) : Server.Services.Interfaces.ITrainRecord;
