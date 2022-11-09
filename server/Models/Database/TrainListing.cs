using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Server.Models.Database;

public record TrainListing(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.ObjectId)]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Id,
    string Rank,
    string Number,
    string Company
) {
    public TrainListing(string rank, string number, string company) : this(null, rank, number, company) { }
}