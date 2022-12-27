using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using scraper.Models.Itinerary;
using Server.Services.Interfaces;

namespace Server.Controllers.V3; 

[ApiController]
[ApiExplorerSettings(GroupName = "v3")]
[Route("/v3/[controller]")]
public class ItinerariesController : Controller {
	private IDataManager DataManager { get; }
	private IDatabase Database { get; }

	public ItinerariesController(IDataManager dataManager, IDatabase database) {
		this.DataManager = dataManager;
		this.Database = database;
	}


	[HttpGet("")]
	[ProducesResponseType(typeof(IEnumerable<IItinerary>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<IEnumerable<IItinerary>>> FindItineraries(
		[FromQuery] string from,
		[FromQuery] string to,
		[FromQuery] DateTimeOffset? date
	) {
		var itineraries = await DataManager.FetchItineraries(from, to, date);

		if (itineraries == null) {
			return NotFound();
		}

		return Ok(itineraries);
	}
}