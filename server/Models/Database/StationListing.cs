using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Server.Models.Database;

public record StationListing(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.ObjectId)]
    [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    string? Id,
    string Name,
    List<string> StoppedAtBy
) {
    public StationListing() : this(null, "", new()) { }
    public StationListing(string name, List<string> stoppedAtBy) : this(null, name, stoppedAtBy) { }
}
