using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Server.Services.Interfaces;

namespace Server.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("/[controller]")]
public class TrainController : Controller {
	private IDataManager DataManager { get; }

	public TrainController(IDataManager dataManager) {
		this.DataManager = dataManager;
	}

	[HttpGet("{trainNumber:int}")]
	public async Task<Models.V1.TrainScrapeResult> TrainInfo(
		[FromRoute] int trainNumber
	) {
		var result = (await DataManager.FetchTrain(
			trainNumber.ToString(),
			DateTimeOffset.Now
		))!;
		return new Models.V1.TrainScrapeResult {
			Date = result.Date,
			Number = result.Number,
			Operator = result.Operator,
			Rank = result.Rank,
			Route = {
				From = result.Groups[0].Route.From,
				To = result.Groups[0].Route.To,
			},
			Stations = result.Groups[0].Stations.Select(station => new Models.V1.TrainStopDescription {
				Arrival = station.Arrival == null
					? null
					: new Models.V1.TrainStopArrDep {
						ScheduleTime = station.Arrival.ScheduleTime.ToString("HH:mm"),
						Status = station.Arrival.Status == null
							? null
							: new Models.V1.Status {
								Delay = station.Arrival.Status.Delay,
								Real = station.Arrival.Status.Real,
							},
					},
				Departure = station.Departure == null
					? null
					: new Models.V1.TrainStopArrDep {
						ScheduleTime = station.Departure.ScheduleTime.ToString("HH:mm"),
						Status = station.Departure.Status == null
							? null
							: new Models.V1.Status {
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
				: new Models.V1.TrainStatus {
					Delay = result.Groups[0].Status!.Delay,
					State = result.Groups[0].Status!.State,
					Station = result.Groups[0].Status!.Station,
				},
		};
	}
}
