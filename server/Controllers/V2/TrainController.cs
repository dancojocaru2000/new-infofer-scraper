using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Server.Services.Interfaces;

namespace Server.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("/v2/[controller]")]
public class TrainController : Controller {
	private IDataManager DataManager { get; }

	public TrainController(IDataManager dataManager) {
		this.DataManager = dataManager;
	}

	[HttpGet("{trainNumber}")]
	public async Task<Models.V2.TrainScrapeResult> TrainInfo(
		[FromRoute] string trainNumber,
		[FromQuery] DateTimeOffset? date = null,
		[FromQuery] string? useYesterday = null
	) {
		if (useYesterday != null &&
			(new string[] { "y", "yes", "t", "true", "1" }).Contains(useYesterday?.Trim()?.ToLower())) {
			date ??= DateTimeOffset.Now.Subtract(TimeSpan.FromDays(1));
		}

		var result = (await DataManager.FetchTrain(trainNumber, date ?? DateTimeOffset.Now))!;
		return new Models.V2.TrainScrapeResult {
			Date = result.Date,
			Number = result.Number,
			Operator = result.Operator,
			Rank = result.Rank,
			Route = {
				From = result.Groups[0].Route.From,
				To = result.Groups[0].Route.To,
			},
			Stations = result.Groups[0].Stations.Select(station => new Models.V2.TrainStopDescription {
				Arrival = station.Arrival == null
					? null
					: new Models.V2.TrainStopArrDep {
						ScheduleTime = station.Arrival.ScheduleTime.ToString("o"),
						Status = station.Arrival.Status == null
							? null
							: new Models.V2.Status {
								Delay = station.Arrival.Status.Delay,
								Real = station.Arrival.Status.Real,
							},
					},
				Departure = station.Departure == null
					? null
					: new Models.V2.TrainStopArrDep {
						ScheduleTime = station.Departure.ScheduleTime.ToString("o"),
						Status = station.Departure.Status == null
							? null
							: new Models.V2.Status {
								Delay = station.Departure.Status.Delay,
								Real = station.Departure.Status.Real,
							},
					},
				Km = station.Km,
				Name = station.Name,
				Platform = station.Platform,
				StoppingTime = station.StoppingTime,
			}).ToList(),
			Status = result.Groups[0].Status == null
				? null
				: new Models.V2.TrainStatus {
					Delay = result.Groups[0].Status!.Delay,
					State = result.Groups[0].Status!.State,
					Station = result.Groups[0].Status!.Station,
				},
		};
	}
}
