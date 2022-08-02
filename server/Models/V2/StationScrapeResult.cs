using System;
using System.Collections.Generic;

namespace Server.Models.V2 {
	public record StationScrapeResult {
		public string Date { get; internal set; } = "";
		public string StationName { get; internal set; } = "";
		public List<StationArrival>? Arrivals { get; internal set; }
		public List<StationDeparture>? Departures { get; internal set; }
	}

	public record StationArrival {
		public int? StoppingTime { get; internal set; }
		public DateTimeOffset Time { get; internal set; }
		public StationArrivalTrain Train { get; internal set; } = new();
	}

	public record StationArrivalTrain {
		public string Number { get; internal set; }
		public string Operator { get; internal set; }
		public string Rank { get; internal set; }
		public List<string> Route { get; internal set; }
		public string Origin { get; internal set; }
	}

	public record StationDeparture {
		public int? StoppingTime { get; internal set; }
		public DateTimeOffset Time { get; internal set; }
		public StationDepartureTrain Train { get; internal set; } = new();
	}

	public record StationDepartureTrain {
		public string Number { get; internal set; }
		public string Operator { get; internal set; }
		public string Rank { get; internal set; }
		public List<string> Route { get; internal set; }
		public string Destination { get; internal set; }
	}
}
