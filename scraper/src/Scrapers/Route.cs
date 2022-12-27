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
using InfoferScraper.Models.Train;
using NodaTime;
using NodaTime.Extensions;
using scraper.Models.Itinerary;

namespace InfoferScraper.Scrapers;

public static class RouteScraper {
	private const string BaseUrl = "https://mersultrenurilor.infofer.ro/ro-RO/";
	private static readonly DateTimeZone BucharestTz = DateTimeZoneProviders.Tzdb["Europe/Bucharest"];

	private static readonly CookieContainer CookieContainer = new();

	private static readonly HttpClient HttpClient = new(new HttpClientHandler {
		CookieContainer = CookieContainer,
		UseCookies = true,
	}) {
		BaseAddress = new Uri(BaseUrl),
		DefaultRequestVersion = new Version(2, 0),
	};
	
	private static readonly Regex KmTrainRankNoRegex = new(@"^([0-9]+)\skm\scu\s([A-Z-]+)\s([0-9]+)$");
	private static readonly Regex OperatorRegex = new(@$"^Operat\sde\s([{Utils.RoLetters}\s]+)$");
	private static readonly Regex DepArrRegex = new(@"^(Ple|Sos)\s([0-9]+)\s([a-z]+)\.?\s([0-9]+):([0-9]+)$");

	private static readonly Dictionary<string, int> Months = new Dictionary<string, int>() {
		["ian"] = 1,
		["feb"] = 2,
		["mar"] = 3,
		["apr"] = 4,
		["mai"] = 5,
		["iun"] = 6,
		["iul"] = 7,
		["aug"] = 8,
		["sep"] = 9,
		["oct"] = 10,
		["noi"] = 11,
		["dec"] = 12,
	};

	public static async Task<List<IItinerary>?> Scrape(string from, string to, DateTimeOffset? dateOverride = null) {
		var dateOverrideInstant = dateOverride?.ToInstant().InZone(BucharestTz);
		dateOverride = dateOverrideInstant?.ToDateTimeOffset();
		TrainScrapeResult result = new();

		var asConfig = Configuration.Default;
		var asContext = BrowsingContext.New(asConfig);

		var firstUrl = "Rute-trenuri"
			.AppendPathSegment(from)
			.AppendPathSegment(to);
		if (dateOverride != null) {
			firstUrl = firstUrl.SetQueryParam("DepartureDate", $"{dateOverride:d.MM.yyyy}");
		}
		firstUrl = firstUrl.SetQueryParam("OrderingTypeId", "0");
		firstUrl = firstUrl.SetQueryParam("TimeSelectionId", "0");
		firstUrl = firstUrl.SetQueryParam("MinutesInDay", "0");
		firstUrl = firstUrl.SetQueryParam("ConnectionsTypeId", "1");
		firstUrl = firstUrl.SetQueryParam("BetweenTrainsMinimumMinutes", "5");
		firstUrl = firstUrl.SetQueryParam("ChangeStationName", "");

		var firstResponse = await HttpClient.GetStringAsync(firstUrl);
		var firstDocument = await asContext.OpenAsync(req => req.Content(firstResponse));
		var firstForm = firstDocument.GetElementById("form-search")!;

		var firstResult = firstForm
			.QuerySelectorAll<IHtmlInputElement>("input")
			.Where(elem => elem.Name != null)
			.ToDictionary(elem => elem.Name!, elem => elem.Value);

		var secondUrl = "".AppendPathSegments("Itineraries", "GetItineraries");
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
        
		var (itineraryInfoDiv, _) = secondDocument
			.QuerySelectorAll("body > div");
        
		if (itineraryInfoDiv == null) {
			return null;
		}

		var itinerariesLi = secondDocument
			.QuerySelectorAll("body > ul > li");
		var itineraries = new List<IItinerary>();
		foreach (var itineraryLi in itinerariesLi) {
			var itinerary = new Itinerary();
            
			var cardDivs = itineraryLi.QuerySelectorAll(":scope > div > div > div > div");
			var detailsDivs = cardDivs[3]
				.QuerySelectorAll(":scope > div > div")[1]
				.QuerySelectorAll(":scope > div");
			var trainItineraryAndDetailsLis = detailsDivs[0]
				.QuerySelectorAll(":scope > ul > li");
			var stations = new List<string>();
			var details = new List<ItineraryTrain>();
			foreach (var (idx, li) in trainItineraryAndDetailsLis.Select((li, idx) => (idx, li))) {
				if (idx % 2 == 0) {
					// Station
					stations.Add(
						li
							.QuerySelectorAll(":scope > div > div > div > div")[1]
							.Text()
							.WithCollapsedSpaces()
					);
				}
				else {
					var now = LocalDateTime.FromDateTime(DateTime.Now);
					// Detail
					var detailColumns = li.QuerySelectorAll(":scope > div > div");
					var leftSideDivs = detailColumns[0].QuerySelectorAll(":scope > div");
					
					var departureDateText = leftSideDivs[0]
						.QuerySelectorAll(":scope > div")[1]
						.Text()
						.WithCollapsedSpaces();
					var departureDateMatch = DepArrRegex.Match(departureDateText);
					var departureDate = new LocalDateTime(
						now.Year,
						Months[departureDateMatch.Groups[3].Value],
						int.Parse(departureDateMatch.Groups[2].Value),
						int.Parse(departureDateMatch.Groups[4].Value),
						int.Parse(departureDateMatch.Groups[5].Value),
						0
					);
					if (departureDate < now.PlusDays(-1)) {
						departureDate = departureDate.PlusYears(1);
					}
					
					var arrivalDateText = leftSideDivs[3]
						.QuerySelectorAll(":scope > div")[1]
						.Text()
						.WithCollapsedSpaces();
					var arrivalDateMatch = DepArrRegex.Match(arrivalDateText);
					var arrivalDate = new LocalDateTime(
						now.Year,
						Months[arrivalDateMatch.Groups[3].Value],
						int.Parse(arrivalDateMatch.Groups[2].Value),
						int.Parse(arrivalDateMatch.Groups[4].Value),
						int.Parse(arrivalDateMatch.Groups[5].Value),
						0
					);
					if (arrivalDate < now.PlusDays(-1)) {
						arrivalDate = arrivalDate.PlusYears(1);
					}

					var rightSideDivs = detailColumns[1].QuerySelectorAll(":scope > div > div");
					var kmRankNumberText = rightSideDivs[0]
						.QuerySelectorAll(":scope > div > div")[0]
						.Text()
						.WithCollapsedSpaces();
					var kmRankNumberMatch = KmTrainRankNoRegex.Match(kmRankNumberText);
					
					var operatorText = rightSideDivs[0]
						.QuerySelectorAll(":scope > div > div")[1]
						.Text()
						.WithCollapsedSpaces();
					var operatorMatch = OperatorRegex.Match(operatorText);

					var train = new ItineraryTrain {
						ArrivalDate = BucharestTz.AtLeniently(arrivalDate).ToDateTimeOffset(),
						DepartureDate = BucharestTz.AtLeniently(departureDate).ToDateTimeOffset(),
						Km = int.Parse(kmRankNumberMatch.Groups[1].Value),
						TrainRank = kmRankNumberMatch.Groups[2].Value,
						TrainNumber = kmRankNumberMatch.Groups[3].Value,
						Operator = operatorMatch.Groups[1].Value,
					};

					foreach (var div in leftSideDivs[2]
						         .QuerySelectorAll(":scope > div")
						         .Where((_, i) => i % 2 != 0)) {
						train.AddIntermediateStop(div.Text().WithCollapsedSpaces());
					}
                    
					details.Add(train);
				}
			}
			foreach (var ((iFrom, iTo), detail) in stations.Zip(stations.Skip(1)).Zip(details)) {
				detail.From = iFrom;
				detail.To = iTo;
				itinerary.AddTrain(detail);
			}
			
			itineraries.Add(itinerary);
		}

		return itineraries;
	}
}