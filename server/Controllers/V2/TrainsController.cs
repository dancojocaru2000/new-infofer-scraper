using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Server.Services.Interfaces;

namespace Server.Controllers.V2;

[ApiController]
[ApiExplorerSettings(GroupName = "v2")]
[Route("/v2/[controller]")]
public class TrainsController : Controller {
	private IDatabase Database { get; }

	public TrainsController(IDatabase database) {
		this.Database = database;
	}

	[HttpGet("")]
	public ActionResult<IEnumerable<ITrainRecord>> ListTrains() {
		return Ok(Database.Trains);
	}
}

