using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InfoferScraper.Models.Train;
using InfoferScraper.Models.Station;
using Server.Services.Interfaces;
using Server.Utils;
using InfoferScraper;

namespace Server.Services.Implementations {
	public class DataManager : IDataManager {
		private IDatabase Database { get; }

		private NodaTime.IDateTimeZoneProvider TzProvider { get; }
		private NodaTime.DateTimeZone CfrTimeZone => TzProvider["Europe/Bucharest"];

		public DataManager(NodaTime.IDateTimeZoneProvider tzProvider, IDatabase database) {
			this.TzProvider = tzProvider;
			this.Database = database;

			stationCache = new(async (t) => {
				var (stationName, date) = t;
				var zonedDate = new NodaTime.LocalDate(date.Year, date.Month, date.Day).AtStartOfDayInZone(CfrTimeZone);

				var station = await InfoferScraper.Scrapers.StationScraper.Scrape(stationName, zonedDate.ToDateTimeOffset());
				if (station != null) {
					await Database.OnStationData(station);
				}
				return station;
			}, TimeSpan.FromMinutes(1));
			trainCache = new(async (t) => {
				var (trainNumber, date) = t;
				var zonedDate = new NodaTime.LocalDate(date.Year, date.Month, date.Day).AtStartOfDayInZone(CfrTimeZone);

				var train = await InfoferScraper.Scrapers.TrainScraper.Scrape(trainNumber, zonedDate.ToDateTimeOffset());
				if (train != null) {
					await Database.OnTrainData(train);
				}
				return train;
			}, TimeSpan.FromSeconds(30));
		}

		private readonly AsyncCache<(string, DateOnly), IStationScrapeResult?> stationCache;
		private readonly AsyncCache<(string, DateOnly), ITrainScrapeResult?> trainCache;

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
	}
}
