using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models.Database;

public record StationAlias(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.ObjectId)]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id,
    string Name,
    [property: BsonRepresentation(BsonType.ObjectId)]
    string? ListingId
) {
    public StationAlias() : this(null, "", null) { }
}
