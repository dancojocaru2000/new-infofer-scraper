using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Server.Models.Database;

public record StationAlias(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.ObjectId)]
    [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    string? Id,
    string Name,
    [property: BsonRepresentation(BsonType.ObjectId)]
    string? ListingId
) {
    public StationAlias() : this(null, "", null) { }
}
