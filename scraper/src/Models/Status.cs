namespace InfoferScraper.Models.Status {
	public interface IStatus {
		public int Delay { get; }

		/// <summary>
		///     Determines whether delay was actually reported or is an approximation
		/// </summary>
		public bool Real { get; }

		public bool Cancelled { get; }
	}

	internal record Status : IStatus {
		public int Delay { get; set; }
		public bool Real { get; set; }
		public bool Cancelled { get; set; }
	}
}
