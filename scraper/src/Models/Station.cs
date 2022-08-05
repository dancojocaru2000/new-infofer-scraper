using System;
using System.Collections.Generic;
using InfoferScraper.Models.Status;

namespace InfoferScraper.Models.Station {
	#region Interfaces
	
	public interface IStationScrapeResult {
		public string StationName { get; }
		/// <summary>
		///     Date in the DD.MM.YYYY format
		///     This date is taken as-is from the result.
		/// </summary>
		public string Date { get; }

		public IReadOnlyList<IStationArrDep>? Arrivals { get; }
		public IReadOnlyList<IStationArrDep>? Departures { get; }
	}

	public interface IStationArrDep {
		public int? StoppingTime { get; }
		public DateTimeOffset Time { get; }
		public IStationTrain Train { get; }
		public IStationStatus Status { get; }
	}

	public interface IStationTrain {
		public string Number { get; }
		public string Operator { get; }
		public string Rank { get; }
		public IReadOnlyList<string> Route { get; }
		/// <summary>
		/// Arrivals -> Departure station; Departures -> Destination station
		/// </summary>
		public string Terminus { get; }
		public DateTimeOffset DepartureDate { get; }
	}

	public interface IStationStatus : IStatus {
		new int Delay { get; }
		new bool Real { get; }
		public bool Cancelled { get; }
		public string? Platform { get; }
	}
	
	#endregion

	#region Implementations
	
	internal record StationScrapeResult : IStationScrapeResult {
		private List<StationArrDep>? _modifyableArrivals = new();
		private List<StationArrDep>? _modifyableDepartures = new();
		
		public string StationName { get; internal set; } = "";
		public string Date { get; internal set; } = "";
		public IReadOnlyList<IStationArrDep>? Arrivals => _modifyableArrivals?.AsReadOnly();
		public IReadOnlyList<IStationArrDep>? Departures => _modifyableDepartures?.AsReadOnly();

		private void AddStationArrival(StationArrDep arrival) {
			_modifyableArrivals ??= new List<StationArrDep>();
			_modifyableArrivals.Add(arrival);
		}

		private void AddStationDeparture(StationArrDep departure) {
			_modifyableDepartures ??= new List<StationArrDep>();
			_modifyableDepartures.Add(departure);
		}

		internal void AddNewStationArrival(Action<StationArrDep> configurator) {
			StationArrDep newStationArrDep = new();
			configurator(newStationArrDep);
			AddStationArrival(newStationArrDep);
		}

		internal void AddNewStationDeparture(Action<StationArrDep> configurator) {
			StationArrDep newStationArrDep = new();
			configurator(newStationArrDep);
			AddStationDeparture(newStationArrDep);
		}
	}

	internal record StationArrDep : IStationArrDep {
		public int? StoppingTime { get; internal set; }
		public DateTimeOffset Time { get; internal set; }
		public IStationTrain Train => ModifyableTrain;
		public IStationStatus Status => ModifyableStatus;

		internal readonly StationTrain ModifyableTrain = new();
		internal readonly StationStatus ModifyableStatus = new();
	}

	internal record StationTrain : IStationTrain {
		private readonly List<string> _modifyableRoute = new();

		public string Number { get; internal set; } = "";
		public string Operator { get; internal set; } = "";
		public string Rank { get; internal set; } = "";
		public IReadOnlyList<string> Route => _modifyableRoute.AsReadOnly();
		public string Terminus { get; internal set; } = "";
		public DateTimeOffset DepartureDate { get; internal set; }

		internal void AddRouteStation(string station) => _modifyableRoute.Add(station);
	}

	internal record StationStatus : IStationStatus {
		public int Delay { get; internal set; }
		public bool Real { get; internal set; }
		public bool Cancelled { get; internal set; }
		public string? Platform { get; internal set; }
	}
	
	#endregion
}
