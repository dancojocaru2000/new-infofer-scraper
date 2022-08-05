using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Flurl;
using InfoferScraper.Models.Station;
using NodaTime;
using NodaTime.Extensions;

namespace InfoferScraper.Scrapers {
	public static class StationScraper {
		private static readonly Regex StationInfoRegex = new($@"^([{Utils.RoLetters}.0-9 ]+)\sîn\s([0-9.]+)$");

		private static readonly Regex StoppingTimeRegex = new(
			@"^(necunoscută \(stație terminus\))|(?:([0-9]+) (min|sec) \((?:începând cu|până la) ([0-9]{1,2}:[0-9]{2})\))$"
		);

		private static readonly Regex StatusRegex = new(
			@"^(?:la timp|([+-]?[0-9]+) min \((?:întârziere|mai devreme)\))(\*?)$"
		);

		private static readonly Regex PlatformRegex = new(@"^linia\s([A-Za-z0-9]+)$");

		private static readonly Regex TrainUrlDateRegex = new(@"Date=([0-9]{2}).([0-9]{2}).([0-9]{4})");
		
		private static readonly DateTimeZone BucharestTz = DateTimeZoneProviders.Tzdb["Europe/Bucharest"];

		private const string BaseUrl = "https://mersultrenurilor.infofer.ro/ro-RO/";

		private static readonly CookieContainer CookieContainer = new();

		private static readonly HttpClient HttpClient = new(new HttpClientHandler {
			CookieContainer = CookieContainer,
			UseCookies = true,
		}) {
			BaseAddress = new Uri(BaseUrl),
			DefaultRequestVersion = new Version(2, 0),
		};

		public static async Task<IStationScrapeResult> Scrape(string stationName, DateTimeOffset? date = null) {
			var dateInstant = date?.ToInstant().InZone(BucharestTz);
			date = dateInstant?.ToDateTimeOffset();
			
			stationName = stationName.RoLettersToEn();

			var result = new StationScrapeResult();

			var asConfig = Configuration.Default;
			var asContext = BrowsingContext.New(asConfig);

			var firstUrl = "Statie"
				.AppendPathSegment(Regex.Replace(stationName, @"\s", "-"));
			if (date != null) {
				firstUrl = firstUrl.SetQueryParam("Date", $"{date:d.MM.yyyy}");
			}
			var firstResponse = await HttpClient.GetStringAsync(firstUrl);
			var firstDocument = await asContext.OpenAsync(req => req.Content(firstResponse));
			var firstForm = firstDocument.GetElementById("form-search")!;

			var firstResult = firstForm
				.QuerySelectorAll<IHtmlInputElement>("input")
				.Where(elem => elem.Name != null)
				.ToDictionary(elem => elem.Name!, elem => elem.Value);

			var secondUrl = "".AppendPathSegments("Stations", "StationsResult");
			var secondResponse = await HttpClient.PostAsync(
				secondUrl,
#pragma warning disable CS8620
				new FormUrlEncodedContent(firstResult)
#pragma warning restore CS8620
			);
			var secondResponseContent = await secondResponse.Content.ReadAsStringAsync();
			var secondDocument = await asContext.OpenAsync(
				req => req.Content(secondResponseContent)
			);

			var (stationInfoDiv, (_, (departuresDiv, (arrivalsDiv, _)))) = secondDocument
				.QuerySelectorAll("body > div");

			(result.StationName, (result.Date, _)) = (StationInfoRegex.Match(
				stationInfoDiv
					.QuerySelector(":scope > h2")!
					.Text()
					.WithCollapsedSpaces()
			).Groups as IEnumerable<Group>).Skip(1).Select(group => group.Value);

			var (dateDay, (dateMonth, (dateYear, _))) = result.Date.Split('.').Select(int.Parse);

			void ParseArrDepList(IElement element, Action<Action<StationArrDep>> adder) {
				Utils.DateTimeSequencer dtSeq = new(dateYear, dateMonth, dateDay);

				if (element.QuerySelector(":scope > div > ul") == null) return;

				foreach (var trainElement in element.QuerySelectorAll(":scope > div > ul > li")) {
					adder(arrDep => {
						var divs = trainElement.QuerySelectorAll(":scope > div");
						var dataDiv = divs[0];
						var statusDiv = divs.Length >= 2 ? divs[1] : null;

						var (dataMainDiv, (dataDetailsDiv, _)) = dataDiv
							.QuerySelectorAll(":scope > div");
						var (timeDiv, (destDiv, (trainDiv, _))) = dataMainDiv
							.QuerySelectorAll(":scope > div");
						var (operatorDiv, (routeDiv, (stoppingTimeDiv, _))) = dataDetailsDiv
							.QuerySelectorAll(":scope > div > div");

						var timeResult = timeDiv
							.QuerySelectorAll(":scope > div > div > div")[1]
							.Text()
							.WithCollapsedSpaces();
						var (stHr, (stMin, _)) = timeResult.Split(':').Select(int.Parse);
						arrDep.Time = BucharestTz.AtLeniently(
							dtSeq.Next(stHr, stMin).ToLocalDateTime()
						).ToDateTimeOffset();

						// ReSharper disable once UnusedVariable // stOppositeTime: might be useful in the future
						var (unknownSt, (st, (minsec, (stOppositeTime, _)))) = (StoppingTimeRegex.Match(
							stoppingTimeDiv.QuerySelectorAll(":scope > div > div")[1]
								.Text()
								.WithCollapsedSpaces()
						).Groups as IEnumerable<Group>).Skip(1).Select(group => group.Value);
						if (unknownSt.Length == 0 && st.Length > 0) {
							arrDep.StoppingTime = int.Parse(st);
							if (minsec == "min") {
								arrDep.StoppingTime *= 60;
							}
						}

						arrDep.ModifyableTrain.Rank = trainDiv
							.QuerySelectorAll(":scope > div > div > div")[1]
							.QuerySelector(":scope > span")!
							.Text()
							.WithCollapsedSpaces();
						arrDep.ModifyableTrain.Number = trainDiv
							.QuerySelectorAll(":scope > div > div > div")[1]
							.QuerySelector(":scope > a")!
							.Text()
							.WithCollapsedSpaces();
						var trainUri = new Uri(
							"http://localhost" + trainDiv
								.QuerySelectorAll(":scope > div > div > div")[1]
								.QuerySelector(":scope > a")!
								.GetAttribute("href")!
						);
						var (trainDepDay, (trainDepMonth, (trainDepYear, _))) = TrainUrlDateRegex
							.Match(trainUri.Query)
							.Groups
							.Values
							.Skip(1)
							.Select(g => int.Parse(g.Value));
						arrDep.ModifyableTrain.DepartureDate = BucharestTz
							.AtStartOfDay(new(trainDepYear, trainDepMonth, trainDepDay))
							.ToDateTimeOffset()
							.ToUniversalTime();
						arrDep.ModifyableTrain.Terminus = destDiv
							.QuerySelectorAll(":scope > div > div > div")[1]
							.Text()
							.WithCollapsedSpaces();
						arrDep.ModifyableTrain.Operator = operatorDiv
							.QuerySelectorAll(":scope > div > div")[1]
							.Text()
							.WithCollapsedSpaces();
						foreach (var station in routeDiv.QuerySelectorAll(":scope > div > div")[1]
							.Text()
							.WithCollapsedSpaces()
							.Split(" - ")) {
							arrDep.ModifyableTrain.AddRouteStation(station);
						}

						if (statusDiv == null) {
							return;
						}

						var statusDivComponents = statusDiv
							.QuerySelectorAll(":scope > div")[0]
							.QuerySelectorAll(":scope > div");

						var delayDiv = statusDivComponents[0];
						
						var (delayMin, (approx, _)) = (StatusRegex.Match(
							delayDiv
								.Text()
								.WithCollapsedSpaces()
						).Groups as IEnumerable<Group>).Skip(1).Select(group => group.Value);
						if (delayMin is null && delayDiv.Text().WithCollapsedSpaces() == "anulat") {
							arrDep.ModifyableStatus.Cancelled = true;
						}
						else if (delayMin is null) {
							throw new Exception($"Unexpected delayDiv value: {delayDiv.Text().WithCollapsedSpaces()}");
						}
						else {
							arrDep.ModifyableStatus.Real = string.IsNullOrEmpty(approx);
							arrDep.ModifyableStatus.Delay = delayMin.Length == 0 ? 0 : int.Parse(delayMin);
						}

						if (statusDivComponents.Length < 2) return;

						var platformDiv = statusDivComponents[1];
						arrDep.ModifyableStatus.Platform = PlatformRegex.Match(platformDiv.Text().WithCollapsedSpaces())
							.Groups[1].Value;
					});
				}
			}

			ParseArrDepList(departuresDiv, result.AddNewStationDeparture);
			ParseArrDepList(arrivalsDiv, result.AddNewStationArrival);

			return result;
		}
	}
}
