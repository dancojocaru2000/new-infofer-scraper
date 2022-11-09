namespace Server.Models.Database;

public record MongoSettings(string ConnectionString, string DatabaseName) {
    public MongoSettings() : this("", "") { }
}
