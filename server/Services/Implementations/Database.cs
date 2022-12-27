using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using InfoferScraper.Models.Station;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using scraper.Models.Itinerary;
using Server.Models.Database;
using Server.Utils;

namespace Server.Services.Implementations;

public class Database : Server.Services.Interfaces.IDatabase {
	private static readonly JsonSerializerOptions serializerOptions = new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	private ILogger<Database> Logger { get; }

	public DbRecord DbData { get; private set; } = new(3);

	public IReadOnlyList<StationListing> Stations => stationListingsCollection
		.Aggregate(PipelineDefinition<StationListing, StationListing>.Create(
			"{ $addFields: { stoppedAtCount: { $size: \"$stoppedAtBy\" } } }",
			"{ $sort: { stoppedAtCount: -1 } }",
			"{ $unset: \"stoppedAtCount\" }"
			))
		.ToList();
	public IReadOnlyList<TrainListing> Trains => trainListingsCollection.FindSync(_ => true).ToList();

	private static readonly string DbDir = Environment.GetEnvironmentVariable("DB_DIR") ?? Path.Join(Environment.CurrentDirectory, "db");
	private static readonly string DbFile = Path.Join(DbDir, "db.json");
	private static readonly string StationsFile = Path.Join(DbDir, "stations.json");
	private static readonly string TrainsFile = Path.Join(DbDir, "trains.json");

	private readonly IMongoDatabase db;
	private readonly IMongoCollection<DbRecord> dbRecordCollection;
	private readonly IMongoCollection<TrainListing> trainListingsCollection;
	private readonly IMongoCollection<StationListing> stationListingsCollection;
	private readonly AsyncThrottle throttle;

	private readonly Dictionary<string, string> trainObjectIds = new();
	private readonly Dictionary<string, string> stationObjectIds = new();

	public Database(ILogger<Database> logger, IOptions<MongoSettings> mongoSettings) {
		Logger = logger;

		var settings = MongoClientSettings.FromConnectionString(mongoSettings.Value.ConnectionString);
		settings.MaxConnectionPoolSize = 10000;
		MongoClient mongoClient = new(settings);
		throttle = new(mongoClient.Settings.MaxConnectionPoolSize / 2);
		db = mongoClient.GetDatabase(mongoSettings.Value.DatabaseName) ?? throw new NullReferenceException("Unable to get Mongo database");
		dbRecordCollection = db.GetCollection<DbRecord>("db");
		trainListingsCollection = db.GetCollection<TrainListing>("trainListings");
		stationListingsCollection = db.GetCollection<StationListing>("stationListings");

		Migration();
	}

	private void Migration() {
		if (!File.Exists(DbFile) && File.Exists(TrainsFile)) {
			Logger.LogInformation("Migrating DB version 1 -> 2");
			if (File.Exists(StationsFile)) {
				Logger.LogDebug("Converting StationsFile");
				var oldStations = JsonNode.Parse(File.ReadAllText(StationsFile));
				List<StationListing> stations = new();
				if (oldStations != null) {
					Logger.LogDebug("Found {StationsCount} stations", oldStations.AsArray().Count);
					foreach (var station in oldStations.AsArray()) {
						if (station == null) continue;
						station["stoppedAtBy"] = new JsonArray(station["stoppedAtBy"]!.AsArray().Select(num => (JsonNode)(num!).ToString()!).ToArray());
					}
					stations = oldStations.Deserialize<List<StationListing>>(serializerOptions)!;
				}
				Logger.LogDebug("Rewriting StationsFile");
				File.WriteAllText(StationsFile, JsonSerializer.Serialize(stations, serializerOptions));
			}
			if (File.Exists(TrainsFile)) {
				Logger.LogDebug("Converting TrainsFile");
				var oldTrains = JsonNode.Parse(File.ReadAllText(TrainsFile));
				List<TrainListing> trains = new();
				if (oldTrains != null) {
					Logger.LogDebug("Found {TrainsCount} trains", oldTrains.AsArray().Count);
					foreach (var train in oldTrains.AsArray()) {
						if (train == null) continue;
						train["number"] = train["numberString"];
						train.AsObject().Remove("numberString");
					}
					trains = oldTrains.Deserialize<List<TrainListing>>(serializerOptions)!;
				}
				Logger.LogDebug("Rewriting TrainsFile");
				File.WriteAllText(TrainsFile, JsonSerializer.Serialize(trains, serializerOptions));
			}
			DbData = new(2);
			File.WriteAllText(DbFile, JsonSerializer.Serialize(DbData, serializerOptions));
			Migration();
		}
		else if (File.Exists(DbFile)) {
			var oldDbData = JsonNode.Parse(File.ReadAllText(DbFile));
			if (((int?)oldDbData?["version"]) == 2) {
				Logger.LogInformation("Migrating DB version 2 -> 3 (transition from fs+JSON to MongoDB)");

				if (File.Exists(StationsFile)) {
					Logger.LogDebug("Converting StationsFile");
					var stations = JsonSerializer.Deserialize<List<StationListing>>(File.ReadAllText(StationsFile));
					stationListingsCollection.InsertMany(stations);
					File.Delete(StationsFile);
				}

				if (File.Exists(TrainsFile)) {
					Logger.LogDebug("Converting TrainsFile");
					var trains = JsonSerializer.Deserialize<List<TrainListing>>(File.ReadAllText(TrainsFile));
					trainListingsCollection.InsertMany(trains);
					File.Delete(TrainsFile);
				}
				
				File.Delete(DbFile);
				try {
					Directory.Delete(DbDir);
				}
				catch (Exception) {
					// Deleting of the directory is optional; may not be allowed in Docker or similar
				}

				var x = dbRecordCollection.FindSync(_ => true).ToList()!;
				if (x.Count != 0) {
					Logger.LogWarning("db collection contained data when migrating to V3");
					using (var _ = Logger.BeginScope("Already existing data:")) {
						foreach (var dbRecord in x) {
							Logger.LogInformation("Id: {Id}, Version: {Version}", dbRecord.Id, dbRecord.Version);
						}
					}
					Logger.LogInformation("Backing up existing data");
					var backupDbRecordCollection = db.GetCollection<DbRecord>("db-backup");
					backupDbRecordCollection.InsertMany(x);
					Logger.LogDebug("Removing existing data");
					dbRecordCollection.DeleteMany(_ => true);
				}
				dbRecordCollection.InsertOne(new(3));
				Migration();
			}
			else {
				throw new("Unexpected Database version, only DB Version 2 uses DbFile");
			}
		}
		else {
			var datas = dbRecordCollection.FindSync(_ => true).ToList();
			if (datas.Count == 0) {
				Logger.LogInformation("No db record found, new database");
				dbRecordCollection.InsertOne(DbData);
			}
			else {
				DbData = datas[0];
			}
			if (DbData.Version == 3) {
				Logger.LogInformation("Using MongoDB Database Version 3; noop");
			}
			else {
				throw new($"Unexpected Database version: {DbData.Version}");
			}
		}
	}

	private readonly SemaphoreSlim insertTrainLock = new (1, 1);
	public async Task<string> FoundTrain(string rank, string number, string company) {
		number = string.Join("", number.TakeWhile(c => c is >= '0' and <= '9'));
		// If there is a matching ObjectId, then it's already in the database
		if (trainObjectIds.ContainsKey(number)) return number;
		await insertTrainLock.WaitAsync();
		try {
			var possibleTrains = await (await throttle.MakeRequest(() => trainListingsCollection.FindAsync(
				Builders<TrainListing>.Filter.Eq("number", number)
			))).ToListAsync();
			if (possibleTrains.Count == 0) {
				Logger.LogDebug("Found train {Rank} {Number} from {Company}", rank, number, company);
				TrainListing listing = new(number: number, rank: rank, company: company);
				await throttle.MakeRequest(() => trainListingsCollection.InsertOneAsync(listing));
				if (listing.Id != null) {
					trainObjectIds[number] = listing.Id;
				}
			}
			else {
				foreach (var possibleTrain in possibleTrains) {
					trainObjectIds[possibleTrain.Number] = possibleTrain.Id!;
				}
			}
		}
		finally {
			insertTrainLock.Release();
		}

		return number;
	}

	private readonly SemaphoreSlim insertStationLock = new (1, 1);

	public async Task FoundStation(string name) {
		// if (!await throttle.MakeRequest(() => stationListingsCollection.Find(Builders<StationListing>.Filter.Eq("name", name)).AnyAsync())) {
		// 	Logger.LogDebug("Found station {StationName}", name);
		// 	await throttle.MakeRequest(() => stationListingsCollection.InsertOneAsync(new(name, new())));
		
		// }
		// If there is a matching ObjectId, then it's already in the database
		if (stationObjectIds.ContainsKey(name)) return;
		
		await insertStationLock.WaitAsync();
		UpdateResult update;
		try {
			update = await stationListingsCollection.UpdateOneAsync(
				Builders<StationListing>.Filter.Eq("name", name),
				Builders<StationListing>.Update.Combine(
					Builders<StationListing>.Update.SetOnInsert("name", name),
					Builders<StationListing>.Update.SetOnInsert("stoppedAtBy", new List<string>())
				),
				new UpdateOptions {
					IsUpsert = true,
				}
			);
			if (update.IsAcknowledged && update.ModifiedCount > 0) {
				stationObjectIds[name] = update.UpsertedId.AsObjectId.ToString();
			}
		}
		finally {
			insertStationLock.Release();
		}

		if (update.IsAcknowledged && update.MatchedCount == 0) {
			Logger.LogDebug("Found station {StationName}", name);
		}
	}

	public async Task FoundStations(IEnumerable<string> names) {
		var unknownStations = names.ToList();
		if (unknownStations.All(s => stationObjectIds.ContainsKey(s))) {
			return;
		}

		unknownStations.RemoveAll(s => stationObjectIds.ContainsKey(s));
		var existingStations = await (await stationListingsCollection.FindAsync(
			Builders<StationListing>.Filter.StringIn("name", unknownStations.Select((n) => new StringOrRegularExpression(n)))
		)).ToListAsync();
		foreach (var existingStation in existingStations) {
			stationObjectIds[existingStation.Name] = existingStation.Id!;
		}

		unknownStations.RemoveAll(s => existingStations.Select(st => st.Name).Contains(s));
		if (unknownStations.Count == 0) return;
		var unknownStationListings = unknownStations.Select((s) => new StationListing(s, new())).ToList();
		await stationListingsCollection.InsertManyAsync(unknownStationListings);
		foreach (var listing in unknownStationListings) {
			stationObjectIds[listing.Name] = listing.Id!;
		}
		Logger.LogDebug("Found stations {StationNames}", unknownStations);
	}

	public async Task FoundTrainAtStation(string stationName, string trainNumber) {
		trainNumber = string.Join("", trainNumber.TakeWhile(c => c is >= '0' and <= '9'));
		await FoundStation(stationName);
		UpdateResult updateResult;
		if (stationObjectIds.ContainsKey(stationName)) {
			updateResult = await throttle.MakeRequest(() => stationListingsCollection.UpdateOneAsync(
				Builders<StationListing>.Filter.Eq("_id", ObjectId.Parse(stationObjectIds[stationName])),
				Builders<StationListing>.Update.AddToSet("stoppedAtBy", trainNumber)
			));
		}
		else {
			updateResult = await throttle.MakeRequest(() => stationListingsCollection.UpdateOneAsync(
				Builders<StationListing>.Filter.Eq("name", stationName),
				Builders<StationListing>.Update.AddToSet("stoppedAtBy", trainNumber)
			));
		}
		if (updateResult.IsAcknowledged && updateResult.ModifiedCount > 0) {
			Logger.LogDebug("Found train {TrainNumber} at station {StationName}", trainNumber, stationName);
		}
	}

	public async Task FoundTrainAtStations(IEnumerable<string> stationNames, string trainNumber) {
		trainNumber = string.Join("", trainNumber.TakeWhile(c => c is >= '0' and <= '9'));
		var enumerable = stationNames as string[] ?? stationNames.ToArray();
		await FoundStations(enumerable);
		var objectIds = enumerable
			.Select<string, ObjectId?>((stationName) => stationObjectIds.ContainsKey(stationName) ? ObjectId.Parse(stationObjectIds[stationName]) : null)
			.ToList();
		UpdateResult updateResult;
		if (!objectIds.Any((id) => id is null)) {
			updateResult = await throttle.MakeRequest(() => stationListingsCollection.UpdateManyAsync(
				Builders<StationListing>.Filter.In("_id", objectIds),
				Builders<StationListing>.Update.AddToSet("stoppedAtBy", trainNumber)
			));
		}
		else {
			updateResult = await throttle.MakeRequest(() => stationListingsCollection.UpdateManyAsync(
				Builders<StationListing>.Filter.StringIn("name", enumerable.Select(sn => new StringOrRegularExpression(sn))),
				Builders<StationListing>.Update.AddToSet("stoppedAtBy", trainNumber)
			));
		}
		if (updateResult.IsAcknowledged && updateResult.ModifiedCount > 0) {
			Logger.LogDebug("Found train {TrainNumber} at stations {StationNames}", trainNumber, stationNames);
		}
	}

	public async Task OnTrainData(InfoferScraper.Models.Train.ITrainScrapeResult trainData) {
		var trainNumber = await FoundTrain(trainData.Rank, trainData.Number, trainData.Operator);
		await FoundTrainAtStations(
			trainData.Groups
			.SelectMany(g => g.Stations)
			.Select(trainStop => trainStop.Name)
			.Distinct(), 
			trainNumber
		);
	}

	public async Task OnStationData(InfoferScraper.Models.Station.IStationScrapeResult stationData) {
		var stationName = stationData.StationName;

		async Task ProcessTrain(InfoferScraper.Models.Station.IStationArrDep train) {
			var trainNumber = train.Train.Number;
			trainNumber = await FoundTrain(train.Train.Rank, trainNumber, train.Train.Operator);
			await FoundTrainAtStations(Enumerable.Repeat(stationName, 1).Concat(train.Train.Route).Distinct(), trainNumber);
		}

		List<IStationArrDep> arrdep = new();
		if (stationData.Arrivals != null) {
			arrdep.AddRange(stationData.Arrivals);
		}
		if (stationData.Departures != null) {
			arrdep.AddRange(stationData.Departures);
		}

		foreach (var train in arrdep.DistinctBy((t) => t.Train.Number)) {
			await ProcessTrain(train);
		}
	}

    public async Task OnItineraries(IReadOnlyList<IItinerary> itineraries) {
        foreach (var itinerary in itineraries) {
            foreach (var train in itinerary.Trains) {
                await FoundTrainAtStations(
                    train.IntermediateStops.Concat(new[] { train.From, train.To }),
                    train.TrainNumber
                );
            }
        }
    }
}

public record DbRecord(
	[property: BsonId]
	[property: BsonRepresentation(BsonType.ObjectId)]
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	string? Id,
	int Version
) {
	public DbRecord(int version) : this(null, version) { }
}
