using System.Collections.Generic;

namespace Server.Models.V2 {
	public record TrainScrapeResult {
		public string Rank { get; internal set; } = "";

		public string Number { get; internal set; } = "";

		/// <summary>
		///     Date in the DD.MM.YYYY format
		///     This date is taken as-is from the result.
		/// </summary>
		public string Date { get; internal set; } = "";

		public string Operator { get; internal set; } = "";

		public TrainRoute Route { get; } = new();

		public TrainStatus? Status { get; internal set; } = new();
		public List<TrainStopDescription> Stations { get; internal set; } = new();
	}

	public record TrainRoute {
		public TrainRoute() {
			From = "";
			To = "";
		}

		public string From { get; set; }
		public string To { get; set; }
	}

	public record TrainStatus {
		public int Delay { get; set; }
		public string Station { get; set; } = "";
		public InfoferScraper.Models.Train.StatusKind State { get; set; }
	}

	public record TrainStopDescription {
		public string Name { get; set; } = "";
		public int Km { get; set; }
		public int? StoppingTime { get; set; }
		public string? Platform { get; set; }
		public TrainStopArrDep? Arrival { get; set; }
		public TrainStopArrDep? Departure { get; set; }
	}

	public record TrainStopArrDep {
		public string ScheduleTime { get; set; } = "";
		public Status? Status { get; set; }
	}

	public record Status {
		public int Delay { get; set; }
		public bool Real { get; set; }
	}
}
