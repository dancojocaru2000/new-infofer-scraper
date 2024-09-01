namespace Server.Models;

public record ProxySettings(string Url, ProxyCredentials? Credentials = null) {
	public ProxySettings() : this("") { }
}

public record ProxyCredentials(string Username, string Password) {
	public ProxyCredentials() : this("", "") { }
}
