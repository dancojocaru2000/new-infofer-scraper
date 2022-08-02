using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfoferScraper.Models.Status;
using InfoferScraper.Models.Train.JsonConverters;

namespace InfoferScraper.Models.Train {
	#region Interfaces

	public interface ITrainScrapeResult {
		public string Rank { get; }

		public string Number { get; }

		/// <summary>
		///     Date in the DD.MM.YYYY format
		///     This date is taken as-is from the result.
		/// </summary>
		public string Date { get; }

		public string Operator { get; }

		public IReadOnlyList<ITrainGroup> Groups { get; }
	}

	public interface ITrainGroup {
		public ITrainRoute Route { get; }

		public ITrainStatus? Status { get; }
		public IReadOnlyList<ITrainStopDescription> Stations { get; }
	}

	public interface ITrainRoute {
		public string From { get; }
		public string To { get; }
	}

	public interface ITrainStatus {
		public int Delay { get; }
		public string Station { get; }
		public StatusKind State { get; }
	}

	public interface ITrainStopDescription {
		public string Name { get; }
		public int Km { get; }

		/// <summary>
		///     The time the train waits in the station in seconds
		/// </summary>
		public int? StoppingTime { get; }

		public string? Platform { get; }
		public ITrainStopArrDep? Arrival { get; }
		public ITrainStopArrDep? Departure { get; }

		public IReadOnlyList<object> Notes { get; }
	}

	public interface ITrainStopNote {
		public NoteKind Kind { get; }
	}

	public interface ITrainStopTrainNumberChangeNote : ITrainStopNote {
		public string Rank { get; }
		public string Number { get; }
	}

	public interface ITrainStopDepartsAsNote : ITrainStopNote {
		public string Rank { get; }
		public string Number { get; }
		public DateTimeOffset DepartureDate { get; }
	}

	public interface ITrainStopDetachingWagonsNote : ITrainStopNote {
		public string Station { get; }
	}

	public interface ITrainStopReceivingWagonsNote : ITrainStopNote {
		public string Station { get; }
	}

	public interface ITrainStopArrDep {
		public DateTimeOffset ScheduleTime { get; }
		public IStatus? Status { get; }
	}

	#endregion

	[JsonConverter(typeof(StatusKindConverter))]
	public enum StatusKind {
		Passing,
		Arrival,
		Departure,
	}

	[JsonConverter(typeof(NoteKindConverter))]
	public enum NoteKind {
		TrainNumberChange,
		DetachingWagons,
		ReceivingWagons,
		DepartsAs,
	}

	#region Implementations

	internal record TrainScrapeResult : ITrainScrapeResult {
		private List<ITrainGroup> ModifyableGroups { get; set; } = new();
		public string Rank { get; set; } = "";
		public string Number { get; set; } = "";
		public string Date { get; set; } = "";
		public string Operator { get; set; } = "";
		public IReadOnlyList<ITrainGroup> Groups => ModifyableGroups.AsReadOnly();

		private void AddTrainGroup(ITrainGroup trainGroup) {
			ModifyableGroups.Add(trainGroup);
		}

		internal void AddTrainGroup(Action<TrainGroup> configurator) {
			TrainGroup newTrainGroup = new();
			configurator(newTrainGroup);
			AddTrainGroup(newTrainGroup);
		}
	}

	internal record TrainGroup : ITrainGroup {
		private List<ITrainStopDescription> ModifyableStations { get; set; } = new();
		public ITrainRoute Route { get; init; } = new TrainRoute();
		public ITrainStatus? Status { get; private set; }
		public IReadOnlyList<ITrainStopDescription> Stations => ModifyableStations.AsReadOnly();

		private void AddStopDescription(ITrainStopDescription stopDescription) {
			ModifyableStations.Add(stopDescription);
		}

		internal void AddStopDescription(Action<TrainStopDescription> configurator) {
			TrainStopDescription newStopDescription = new();
			configurator(newStopDescription);
			AddStopDescription(newStopDescription);
		}

		internal void ConfigureRoute(Action<TrainRoute> configurator) {
			configurator((TrainRoute)Route);
		}

		internal void MakeStatus(Action<TrainStatus> configurator) {
			TrainStatus newStatus = new();
			configurator(newStatus);
			Status = newStatus;
		}
	}

	internal record TrainRoute : ITrainRoute {
		public TrainRoute() {
			From = "";
			To = "";
		}

		public string From { get; set; }
		public string To { get; set; }
	}

	internal record TrainStatus : ITrainStatus {
		public int Delay { get; set; }
		public string Station { get; set; } = "";
		public StatusKind State { get; set; }
	}

	internal record TrainStopDescription : ITrainStopDescription {
		private List<ITrainStopNote> ModifyableNotes { get; } = new();
		public string Name { get; set; } = "";
		public int Km { get; set; }
		public int? StoppingTime { get; set; }
		public string? Platform { get; set; }
		public ITrainStopArrDep? Arrival { get; private set; }
		public ITrainStopArrDep? Departure { get; private set; }
		public IReadOnlyList<object> Notes => ModifyableNotes.AsReadOnly();

		internal void MakeArrival(Action<TrainStopArrDep> configurator) {
			TrainStopArrDep newArrival = new();
			configurator(newArrival);
			Arrival = newArrival;
		}

		internal void MakeDeparture(Action<TrainStopArrDep> configurator) {
			TrainStopArrDep newDeparture = new();
			configurator(newDeparture);
			Departure = newDeparture;
		}

		class DepartsAsNote : ITrainStopDepartsAsNote {
			public NoteKind Kind => NoteKind.DepartsAs;
			public string Rank { get; set; } = "";
			public string Number { get; set; } = "";
			public DateTimeOffset DepartureDate { get; set; }
		}

		class TrainNumberChangeNote : ITrainStopTrainNumberChangeNote {
			public NoteKind Kind => NoteKind.TrainNumberChange;
			public string Rank { get; set; } = "";
			public string Number { get; set; } = "";
		}

		class ReceivingWagonsNote : ITrainStopReceivingWagonsNote {
			public NoteKind Kind => NoteKind.ReceivingWagons;
			public string Station { get; set; } = "";
		}

		class DetachingWagonsNote : ITrainStopReceivingWagonsNote {
			public NoteKind Kind => NoteKind.DetachingWagons;
			public string Station { get; set; } = "";
		}

		internal void AddDepartsAsNote(string rank, string number, DateTimeOffset departureDate) {
			ModifyableNotes.Add(new DepartsAsNote { Rank = rank, Number = number, DepartureDate = departureDate });
		}

		internal void AddTrainNumberChangeNote(string rank, string number) {
			ModifyableNotes.Add(new TrainNumberChangeNote { Rank = rank, Number = number });
		}

		internal void AddReceivingWagonsNote(string station) {
			ModifyableNotes.Add(new ReceivingWagonsNote { Station = station });
		}

		internal void AddDetachingWagonsNote(string station) {
			ModifyableNotes.Add(new DetachingWagonsNote { Station = station });
		}
	}

	public record TrainStopArrDep : ITrainStopArrDep {
		public DateTimeOffset ScheduleTime { get; set; }
		public IStatus? Status { get; private set; }

		internal void MakeStatus(Action<Status.Status> configurator) {
			Status.Status newStatus = new();
			configurator(newStatus);
			Status = newStatus;
		}
	}

	#endregion

	#region JSON Converters

	namespace JsonConverters {
		internal class StatusKindConverter : JsonConverterFactory {
			public override bool CanConvert(Type typeToConvert) {
				return typeToConvert == typeof(StatusKind);
			}

			public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
				return new Converter();
			}

			private class Converter : JsonConverter<StatusKind> {
				public override StatusKind Read(
					ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options
				) {
					return reader.GetString() switch {
						"arrival" => StatusKind.Arrival,
						"departure" => StatusKind.Departure,
						"passing" => StatusKind.Passing,
						_ => throw new NotImplementedException()
					};
				}

				public override void Write(Utf8JsonWriter writer, StatusKind value, JsonSerializerOptions options) {
					writer.WriteStringValue(value switch {
						StatusKind.Passing => "passing",
						StatusKind.Arrival => "arrival",
						StatusKind.Departure => "departure",
						_ => throw new NotImplementedException()
					});
				}
			}
		}

		internal class NoteKindConverter : JsonConverterFactory {
			public override bool CanConvert(Type typeToConvert) {
				return typeToConvert == typeof(NoteKind);
			}

			public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
				return new Converter();
			}

			private class Converter : JsonConverter<NoteKind> {
				public override NoteKind Read(
					ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options
				) {
					return reader.GetString() switch {
						"departsAs" => NoteKind.DepartsAs,
						"trainNumberChange" => NoteKind.TrainNumberChange,
						"receivingWagons" => NoteKind.ReceivingWagons,
						"detachingWagons" => NoteKind.DetachingWagons,
						_ => throw new NotImplementedException()
					};
				}

				public override void Write(Utf8JsonWriter writer, NoteKind value, JsonSerializerOptions options) {
					writer.WriteStringValue(value switch {
						NoteKind.DepartsAs => "departsAs",
						NoteKind.TrainNumberChange => "trainNumberChange",
						NoteKind.DetachingWagons => "detachingWagons",
						NoteKind.ReceivingWagons => "receivingWagons",
						_ => throw new NotImplementedException()
					});
				}
			}
		}
	}

	#endregion
}
