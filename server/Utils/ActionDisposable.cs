using System;

namespace Server.Utils;

public class ActionDisposable : IDisposable {
	public Action Action { get; init; }

	public ActionDisposable(Action action) {
		Action = action;
	}

	public void Dispose() {
		Action();
	}
}
