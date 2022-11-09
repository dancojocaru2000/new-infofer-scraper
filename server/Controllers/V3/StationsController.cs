using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InfoferScraper.Models.Station;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Server.Models.Database;
using Server.Services.Interfaces;

namespace Server.Controllers.V3;

[ApiController]
[ApiExplorerSettings(GroupName = "v3")]
[Route("/v3/[controller]")]
public class StationsController : Controller {
	private IDataManager DataManager { get; }
	private IDatabase Database { get; }

	public StationsController(IDataManager dataManager, IDatabase database) {
		this.DataManager = dataManager;
		this.Database = database;
	}

	[HttpGet("")]
	public ActionResult<IEnumerable<StationListing>> ListStations() {
		return Ok(Database.Stations);
	}

	[HttpGet("{stationName}")]
	[ProducesResponseType(typeof(IStationScrapeResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<IStationScrapeResult>> StationInfo(
		[FromRoute] string stationName,
		[FromQuery] DateTimeOffset? date = null,
		[FromQuery] string? lastUpdateId = null
	) {
		var result = await DataManager.FetchStation(stationName, date ?? DateTimeOffset.Now);
		if (result == null) {
			return NotFound(new {
				Reason = "station_not_found",
			});
		}
		return Ok(result);
	}
}
