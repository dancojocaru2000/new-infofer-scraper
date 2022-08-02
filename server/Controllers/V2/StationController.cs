using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Server.Services.Interfaces;
using Server.Models.V2;

namespace Server.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("/v2/[controller]")]
public class StationController : Controller {
	private IDataManager DataManager { get; }

	public StationController(IDataManager dataManager) {
		this.DataManager = dataManager;
	}

	[HttpGet("{stationName}")]
	public async Task<Models.V2.StationScrapeResult> StationInfo([FromRoute] string stationName) {
		var result = (await DataManager.FetchStation(stationName, DateTimeOffset.Now))!;
		return new StationScrapeResult {
			Date = result.Date,
			StationName = result.StationName,
			Arrivals = result.Arrivals?.Select(arrival => new StationArrival {
				Time = arrival.Time,
				StoppingTime = arrival.StoppingTime,
				Train = new StationArrivalTrain {
					Number	= arrival.Train.Number,
					Operator = arrival.Train.Operator,
					Origin = arrival.Train.Terminus,
					Rank = arrival.Train.Rank,
					Route = arrival.Train.Route.ToList(),
				},
			})?.ToList(),
			Departures = result.Departures?.Select(departure => new StationDeparture {
				Time = departure.Time,
				StoppingTime = departure.StoppingTime,
				Train = new StationDepartureTrain {
					Number	= departure.Train.Number,
					Operator = departure.Train.Operator,
					Destination = departure.Train.Terminus,
					Rank = departure.Train.Rank,
					Route = departure.Train.Route.ToList(),
				},
			})?.ToList(),
		};
	}
}
