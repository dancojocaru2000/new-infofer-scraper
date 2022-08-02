using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace scraper.Exceptions {
	/// <summary>
	/// The train that the information was requested for might be running,
	/// but it is not running on the requested day.
	/// </summary>
	public class TrainNotThisDayException : Exception {
		public TrainNotThisDayException() : base() { }
		protected TrainNotThisDayException([NotNull] SerializationInfo info, StreamingContext context) : base(info, context) { }
		public TrainNotThisDayException([CanBeNull] string? message) : base(message) { }
		public TrainNotThisDayException([CanBeNull] string? message, [CanBeNull] Exception? innerException) : base(message, innerException) { }
	}
}