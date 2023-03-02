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
using scraper.Exceptions;

namespace InfoferScraper.Scrapers {
	public static class TrainScraper {
		private const string BaseUrl = "https://mersultrenurilor.infofer.ro/ro-RO/";
		private static readonly Regex TrainInfoRegex = new(@"^([A-Z-]+)\s([0-9]+)\sîn\s([0-9.]+)$");
		private static readonly Regex OperatorRegex = new(@"^Operat\sde\s(.+)$");

		private static readonly Regex RouteRegex =
			new(@$"^Parcurs\stren\s([{Utils.RoLetters} ]+)[-–]([{Utils.RoLetters}\s]+)$");

		private static readonly Regex SlRegex =
			new(
				@"^(?:Fără|([0-9]+)\smin)\s(întârziere|mai\sdevreme)\sla\s(trecerea\sfără\soprire\sprin|sosirea\sîn|plecarea\sdin)\s(.+)\.$");

		private static readonly Dictionary<char, StatusKind> SlStateMap = new() {
			{ 't', StatusKind.Passing },
			{ 's', StatusKind.Arrival },
			{ 'p', StatusKind.Departure },
		};

		private static readonly Regex KmRegex = new(@"^km\s([0-9]+)$");
		private static readonly Regex StoppingTimeRegex = new(@"^([0-9]+)\s(min|sec)\soprire$");
		private static readonly Regex PlatformRegex = new(@"^linia\s(.+)$");

		private static readonly Regex StationArrdepStatusRegex =
			new(@"^(?:(la timp)|(?:((?:\+|-)[0-9]+) min \((?:(?:întârziere)|(?:mai devreme))\)))(\*?)$");

		private static readonly Regex TrainNumberChangeNoteRegex = 
			new(@"^Trenul își schimbă numărul în\s([A-Z-]+)\s([0-9]+)$");
		private static readonly Regex DepartsAsNoteRegex = 
			new(@"^Trenul pleacă cu numărul\s([A-Z-]+)\s([0-9]+)\sîn\s([0-9]{2}).([0-9]{2}).([0-9]{4})$");
		private static readonly Regex ReceivingWagonsNoteRegex = 
			new(@"^Trenul primește vagoane de la\s(.+)\.$");
		private static readonly Regex DetachingWagonsNoteRegex = 
			new(@"^Trenul detașează vagoane pentru stația\s(.+)\.$");

		private static readonly DateTimeZone BucharestTz = DateTimeZoneProviders.Tzdb["Europe/Bucharest"];

		private static readonly CookieContainer CookieContainer = new();
		private static readonly HttpClient HttpClient = new(new HttpClientHandler {
			CookieContainer = CookieContainer,
			UseCookies = true,
		}) {
			BaseAddress = new Uri(BaseUrl),
			DefaultRequestVersion = new Version(2, 0),
		};

		public static async Task<ITrainScrapeResult?> Scrape(string trainNumber, DateTimeOffset? dateOverride = null) {
			var dateOverrideInstant = dateOverride?.ToInstant().InZone(BucharestTz);
			dateOverride = dateOverrideInstant?.ToDateTimeOffset();
			TrainScrapeResult result = new();

			var asConfig = Configuration.Default;
			var asContext = BrowsingContext.New(asConfig);

			var firstUrl = "Tren"
				.AppendPathSegment(trainNumber);
			if (dateOverride != null) {
				firstUrl = firstUrl.SetQueryParam("Date", $"{dateOverride:d.MM.yyyy}");
			}
			var firstResponse = await HttpClient.GetStringAsync(firstUrl);
			var firstDocument = await asContext.OpenAsync(req => req.Content(firstResponse));
			var firstForm = firstDocument.GetElementById("form-search")!;

			var firstResult = firstForm
				.QuerySelectorAll<IHtmlInputElement>("input")
				.Where(elem => elem.Name != null)
				.ToDictionary(elem => elem.Name!, elem => elem.Value);

			var secondUrl = "".AppendPathSegments("Trains", "TrainsResult");
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

			var (trainInfoDiv, (_, (_, (resultsDiv, _)))) = secondDocument
				.QuerySelectorAll("body > div");
			if (trainInfoDiv == null) {
				return null;
			}
			if (resultsDiv == null) {
				throw new TrainNotThisDayException();
			}
			trainInfoDiv = trainInfoDiv.QuerySelectorAll(":scope > div > div").First();

			(result.Rank, (result.Number, (result.Date, _))) = (TrainInfoRegex.Match(
				trainInfoDiv.QuerySelector(":scope > h2")!.Text().WithCollapsedSpaces()
			).Groups as IEnumerable<Group>).Select(group => group.Value).Skip(1);
			var (scrapedDateD, (scrapedDateM, (scrapedDateY, _))) = result.Date
				.Split('.')
				.Select(int.Parse);
			var date = new DateTime(scrapedDateY, scrapedDateM, scrapedDateD);

			result.Operator = (OperatorRegex.Match(
				trainInfoDiv.QuerySelector(":scope > p")!.Text().WithCollapsedSpaces()
			).Groups as IEnumerable<Group>).Skip(1).First().Value;

			foreach (var groupDiv in resultsDiv.QuerySelectorAll(":scope > div")) {
				result.AddTrainGroup(group => {
					var statusDiv = groupDiv.QuerySelectorAll(":scope > div").First();
					var routeText = statusDiv.QuerySelector(":scope > h4")!.Text().WithCollapsedSpaces();
					group.ConfigureRoute(route => {
						(route.From, (route.To, _)) = (RouteRegex.Match(routeText).Groups as IEnumerable<Group>).Skip(1)
							.Select(group => group.Value);
					});

					try {
						var statusLineMatch =
							SlRegex.Match(statusDiv.QuerySelector(":scope > div")!.Text().WithCollapsedSpaces());
						var (slmDelay, (slmLate, (slmArrival, (slmStation, _)))) =
							(statusLineMatch.Groups as IEnumerable<Group>).Skip(1).Select(group => group.Value);
						group.MakeStatus(status => {
							status.Delay = string.IsNullOrEmpty(slmDelay) ? 0 :
								slmLate == "întârziere" ? int.Parse(slmDelay) : -int.Parse(slmDelay);
							status.Station = slmStation;
							status.State = SlStateMap[slmArrival[0]];
						});
					}
					catch {
						// ignored
					}

					Utils.DateTimeSequencer dtSeq = new(date.Year, date.Month, date.Day);
					var stations = statusDiv.QuerySelectorAll(":scope > ul > li");
					foreach (var station in stations) {
						group.AddStopDescription(stopDescription => {
							var (left, (middle, (right, _))) = station
								.QuerySelectorAll(":scope > div > div");
							var (stopDetails, (stopNotes, _)) = middle
								.QuerySelectorAll(":scope > div > div > div");
							stopDescription.Name = stopDetails
								.QuerySelectorAll(":scope > div")[0]
								.Text()
								.WithCollapsedSpaces();
							stopDescription.LinkName = new Flurl.Url(stopDetails
								.QuerySelectorAll(":scope > div")[0]
								.QuerySelector(":scope a")
								.Attributes["href"]
								.Value).PathSegments.Last();
							var scrapedKm = stopDetails
								.QuerySelectorAll(":scope > div")[1]
								.Text()
								.WithCollapsedSpaces();
							stopDescription.Km = int.Parse(
								(KmRegex.Match(scrapedKm).Groups as IEnumerable<Group>).Skip(1).First().Value
							);
							var scrapedStoppingTime = stopDetails
								.QuerySelectorAll(":scope > div")[2]
								.Text()
								.WithCollapsedSpaces();
							if (!string.IsNullOrEmpty(scrapedStoppingTime)) {
								var (stValue, (stMinsec, _)) =
									(StoppingTimeRegex.Match(scrapedStoppingTime).Groups as IEnumerable<Group>)
									.Skip(1)
									.Select(group => group.Value);
								stopDescription.StoppingTime = int.Parse(stValue);
								if (stMinsec == "min") stopDescription.StoppingTime *= 60;
							}

							var scrapedPlatform = stopDetails
								.QuerySelectorAll(":scope > div")[3]
								.Text()
								.WithCollapsedSpaces();
							if (!string.IsNullOrEmpty(scrapedPlatform))
								stopDescription.Platform = PlatformRegex.Match(scrapedPlatform).Groups[1].Value;

							void ScrapeTime(IElement element, ref TrainStopArrDep arrDep) {
								var parts = element.QuerySelectorAll(":scope > div > div > div");
								if (parts.Length == 0) throw new OperationCanceledException();
								var time = parts[0];
								var scrapedTime = time.Text().WithCollapsedSpaces();
								var (stHour, (stMin, _)) = scrapedTime.Split(':').Select(int.Parse);
								arrDep.ScheduleTime = BucharestTz.AtLeniently(dtSeq.Next(stHour, stMin).ToLocalDateTime())
									.ToDateTimeOffset();

								if (parts.Length < 2) return;

								var statusElement = parts[1];
								var (onTime, (delay, (approx, _))) = (StationArrdepStatusRegex.Match(
									statusElement.Text().WithCollapsedSpaces(replaceWith: " ")
								).Groups as IEnumerable<Group>).Skip(1).Select(group => group.Value);
								arrDep.MakeStatus(status => {
									if (string.IsNullOrEmpty(onTime) && delay == null) {
										status.Cancelled = true;
									}
									else {
										status.Delay = string.IsNullOrEmpty(onTime) ? int.Parse(delay) : 0;
									}
									status.Real = string.IsNullOrEmpty(approx);
								});
							}

							try {
								stopDescription.MakeArrival(arrival => { ScrapeTime(left, ref arrival); });
							}
							catch (OperationCanceledException) { }

							try {
								stopDescription.MakeDeparture(departure => { ScrapeTime(right, ref departure); });
							}
							catch (OperationCanceledException) { }

							foreach (var noteDiv in stopNotes.QuerySelectorAll(":scope > div > div")) {
								var noteText = noteDiv.Text().WithCollapsedSpaces();
								Match trainNumberChangeMatch, departsAsMatch, detachingWagons, receivingWagons;
								if ((trainNumberChangeMatch = TrainNumberChangeNoteRegex.Match(noteText)).Success) {
									stopDescription.AddTrainNumberChangeNote(trainNumberChangeMatch.Groups[1].Value, trainNumberChangeMatch.Groups[2].Value);
								}
								else if ((departsAsMatch = DepartsAsNoteRegex.Match(noteText)).Success) {
									var groups = departsAsMatch.Groups;
									var departureDate = BucharestTz.AtStrictly(new(int.Parse(groups[5].Value), int.Parse(groups[4].Value), int.Parse(groups[3].Value), 0, 0));
									stopDescription.AddDepartsAsNote(groups[1].Value, groups[2].Value, departureDate.ToDateTimeOffset());
								}
								else if ((detachingWagons = DetachingWagonsNoteRegex.Match(noteText)).Success) {
									stopDescription.AddDetachingWagonsNote(detachingWagons.Groups[1].Value);
								}
								else if ((receivingWagons = ReceivingWagonsNoteRegex.Match(noteText)).Success) {
									stopDescription.AddReceivingWagonsNote(receivingWagons.Groups[1].Value);
								}
							}
						});
					}
				});
			}
			return result;
		}
	}
} // namespace
