using System;
using System.Collections.Generic;

namespace scraper.Models.Itinerary; 

#region Interfaces

public interface IItinerary {
	public IReadOnlyList<IItineraryTrain> Trains { get; }
}

public interface IItineraryTrain {
	public string From { get; }
	public string To { get; }
	public IReadOnlyList<string> IntermediateStops { get; }
	public DateTimeOffset DepartureDate { get; }
	public DateTimeOffset ArrivalDate { get; }
	public int Km { get; }
	public string Operator { get; }
	public string TrainRank { get; }
	public string TrainNumber { get; }
}

#endregion

#region Implementations

internal record Itinerary : IItinerary {
	private List<IItineraryTrain> ModifyableTrains { get; set; } = new();
	
	public IReadOnlyList<IItineraryTrain> Trains => ModifyableTrains;

	internal void AddTrain(IItineraryTrain train) {
		ModifyableTrains.Add(train);
	}

	internal void AddTrain(Action<ItineraryTrain> configurator) {
		ItineraryTrain newTrain = new();
		configurator(newTrain);
		AddTrain(newTrain);
	}
}

internal record ItineraryTrain : IItineraryTrain {
	private List<string> ModifyableIntermediateStops { get; set; } = new();
	
	public string From { get; internal set; } = "";
	public string To { get; internal set; } = "";
	public IReadOnlyList<string> IntermediateStops => ModifyableIntermediateStops;
	public DateTimeOffset DepartureDate { get; internal set; } = new();
	public DateTimeOffset ArrivalDate { get; internal set; } = new();
	public int Km { get; internal set; } = 0;
	public string Operator { get; internal set; } = "";
	public string TrainRank { get; internal set; } = "";
	public string TrainNumber { get; internal set; } = "";

	internal void AddIntermediateStop(string stop) {
		ModifyableIntermediateStops.Add(stop);
	}
}

#endregion