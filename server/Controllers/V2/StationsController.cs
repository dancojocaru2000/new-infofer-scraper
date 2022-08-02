using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Server.Services.Interfaces;

namespace Server.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("/v2/[controller]")]
public class StationsController : Controller {
	private IDatabase Database { get; }

	public StationsController(IDatabase database) {
		this.Database = database;
	}

	[HttpGet("")]
	public ActionResult<IEnumerable<IStationRecord>> ListStations() {
		return Ok(Database.Stations);
	}
}
