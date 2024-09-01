namespace Server.Models;

public record ProxySettings(bool UseProxy, string Url, ProxyCredentials? Credentials = null) {
	public ProxySettings() : this(false, "") { }
}

public record ProxyCredentials(string Username, string Password) {
	public ProxyCredentials() : this("", "") { }
}
