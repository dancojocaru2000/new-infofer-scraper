using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Server.Services.Interfaces;

namespace Server.Controllers.V1;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("/[controller]")]
public class TrainsController : Controller {
	private IDatabase Database { get; }

	public TrainsController(IDatabase database) {
		this.Database = database;
	}

	[HttpGet("")]
	public ActionResult<IEnumerable<string>> ListTrains() {
		return Ok(Database.Trains.Select(train => train.Number));
	}
}
