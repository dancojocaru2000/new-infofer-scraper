using System.Collections.Generic;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models.Database;

public record StationListing(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.ObjectId)]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id,
    string Name,
    List<string> StoppedAtBy
) {
    public StationListing() : this(null, "", new()) { }
    public StationListing(string name, List<string> stoppedAtBy) : this(null, name, stoppedAtBy) { }
}
