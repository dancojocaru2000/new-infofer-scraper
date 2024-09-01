using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using InfoferScraper;
using InfoferScraper.Models.Station;
using InfoferScraper.Models.Train;
using Microsoft.Extensions.Logging;
using scraper.Models.Itinerary;
using Server.Models;
using Server.Services.Interfaces;
using Server.Utils;

namespace Server.Services.Implementations {
	public class DataManager : IDataManager {
		private ILogger<DataManager> Logger { get; }
		private IDatabase Database { get; }

		private NodaTime.IDateTimeZoneProvider TzProvider { get; }
		private NodaTime.DateTimeZone CfrTimeZone => TzProvider["Europe/Bucharest"];

		public DataManager(NodaTime.IDateTimeZoneProvider tzProvider, IDatabase database, ILogger<DataManager> logger, ProxySettings? proxySettings) {
			this.TzProvider = tzProvider;
			this.Database = database;
			this.Logger = logger;

			HttpClientHandler httpClientHandler = new (){
				UseProxy = proxySettings != null,
				Proxy = proxySettings == null ? null : new WebProxy(proxySettings.Url),
				DefaultProxyCredentials = proxySettings?.Credentials == null ? null : new NetworkCredential(proxySettings.Credentials.Username, proxySettings.Credentials.Password),
			};
			InfoferScraper.Scrapers.StationScraper stationScraper = new(httpClientHandler);
			InfoferScraper.Scrapers.TrainScraper trainScraper = new(httpClientHandler);
			InfoferScraper.Scrapers.RouteScraper routeScraper = new(httpClientHandler);

			stationCache = new(async (t) => {
				var (stationName, date) = t;
				Logger.LogDebug("Fetching station {StationName} for date {Date}", stationName, date);
				var zonedDate = new NodaTime.LocalDate(date.Year, date.Month, date.Day).AtStartOfDayInZone(CfrTimeZone);

				var station = await stationScraper.Scrape(stationName, zonedDate.ToDateTimeOffset());
				if (station != null) {
					_ = Task.Run(async () => {
						var watch = Stopwatch.StartNew();
						await Database.OnStationData(station);
						var ms = watch.ElapsedMilliseconds;
						Logger.LogInformation("OnStationData timing: {StationDataMs} ms", ms);
					});
				}
				return station;
			}, TimeSpan.FromMinutes(1));
			trainCache = new(async (t) => {
				var (trainNumber, date) = t;
				Logger.LogDebug("Fetching train {TrainNumber} for date {Date}", trainNumber, date);
				var zonedDate = new NodaTime.LocalDate(date.Year, date.Month, date.Day).AtStartOfDayInZone(CfrTimeZone);

				var train = await trainScraper.Scrape(trainNumber, zonedDate.ToDateTimeOffset());
				if (train != null) {
					_ = Task.Run(async () => {
						var watch = Stopwatch.StartNew();
						await Database.OnTrainData(train);
						var ms = watch.ElapsedMilliseconds;
						Logger.LogInformation("OnTrainData timing: {StationDataMs} ms", ms);
					});
				}
				return train;
			}, TimeSpan.FromSeconds(30));
			itinerariesCache = new(async (t) => {
				var (from, to, date) = t;
				Logger.LogDebug("Fetching itinerary from {From} to {To} for date {Date}", from, to, date);
				var zonedDate = new NodaTime.LocalDate(date.Year, date.Month, date.Day).AtStartOfDayInZone(CfrTimeZone);

				var itineraries = await routeScraper.Scrape(from, to, zonedDate.ToDateTimeOffset());
				if (itineraries != null) {
					_ = Task.Run(async () => {
						var watch = Stopwatch.StartNew();
						await Database.OnItineraries(itineraries);
						var ms = watch.ElapsedMilliseconds;
						Logger.LogInformation("OnItineraries timing: {StationDataMs} ms", ms);
					});
				}

				return itineraries;
			}, TimeSpan.FromMinutes(1));
		}

		private readonly AsyncCache<(string, DateOnly), IStationScrapeResult?> stationCache;
		private readonly AsyncCache<(string, DateOnly), ITrainScrapeResult?> trainCache;
		private readonly AsyncCache<(string, string, DateOnly), IReadOnlyList<IItinerary>?> itinerariesCache;

		public Task<IStationScrapeResult?> FetchStation(string stationName, DateTimeOffset date) {
			var cfrDateTime = new NodaTime.ZonedDateTime(NodaTime.Instant.FromDateTimeOffset(date), CfrTimeZone);
			var cfrDate = new DateOnly(cfrDateTime.Year, cfrDateTime.Month, cfrDateTime.Day);

			return stationCache.GetItem((stationName.RoLettersToEn().ToLowerInvariant(), cfrDate));
		}

		public Task<ITrainScrapeResult?> FetchTrain(string trainNumber, DateTimeOffset date) {
			var cfrDateTime = new NodaTime.ZonedDateTime(NodaTime.Instant.FromDateTimeOffset(date), CfrTimeZone);
			var cfrDate = new DateOnly(cfrDateTime.Year, cfrDateTime.Month, cfrDateTime.Day);

			return trainCache.GetItem((trainNumber, cfrDate));
		}

		public async Task<IReadOnlyList<IItinerary>?> FetchItineraries(string from, string to, DateTimeOffset? date = null) {
			var cfrDateTime = new NodaTime.ZonedDateTime(NodaTime.Instant.FromDateTimeOffset(date ?? DateTimeOffset.Now), CfrTimeZone);
			var cfrDate = new DateOnly(cfrDateTime.Year, cfrDateTime.Month, cfrDateTime.Day);

			return await itinerariesCache.GetItem((from, to, cfrDate));
		}
	}
}
