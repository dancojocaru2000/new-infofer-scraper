using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InfoferScraper.Models.Train;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using scraper.Exceptions;
using Server.Models.Database;
using Server.Services.Interfaces;

namespace Server.Controllers.V3;

[ApiController]
[ApiExplorerSettings(GroupName = "v3")]
[Route("/v3/[controller]")]
public class TrainsController : Controller {
	private IDataManager DataManager { get; }
	private IDatabase Database { get; }

	public TrainsController(IDataManager dataManager, IDatabase database) {
		this.DataManager = dataManager;
		this.Database = database;
	}

	[HttpGet("")]
	public ActionResult<IEnumerable<TrainListing>> ListTrains() {
		return Ok(Database.Trains);
	}

	/// <summary>
	/// Searches for information about a train
	/// </summary>
	/// <param name="trainNumber">The number of the train, without additional things such as the rank</param>
	/// <param name="date">The date when the train departs from the first station</param>
	/// <returns>Information about the train</returns>
	/// <response code="404">If the train number requested cannot be found (invalid or not running on the requested date)</response>
	[HttpGet("{trainNumber}")]
	[ProducesResponseType(typeof(ITrainScrapeResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<ITrainScrapeResult>> TrainInfoV3(
		[FromRoute] string trainNumber,
		[FromQuery] DateTimeOffset? date = null
	) {
		try {
			var result = await DataManager.FetchTrain(trainNumber, date ?? DateTimeOffset.Now);
			if (result == null) {
				return NotFound(new {
					Reason = "train_not_found",
				});
			}
			return Ok(result);
		} catch (TrainNotThisDayException) {
			return NotFound(new {
				Reason = "not_running_today",
			});
		}
		// var (token, result) = await DataManager.GetNewTrainDataUpdate(
		// 	trainNumber,
		// 	date ?? DateTimeOffset.Now,
		// 	lastUpdateId ?? ""
		// );
		// Response.Headers.Add("X-Update-Id", new StringValues(token));
		// return Ok(result);
	}


}
