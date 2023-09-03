using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Server.Models.Database;

public record TrainListing(
    [property: BsonId]
    [property: BsonRepresentation(BsonType.ObjectId)]
    [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    string? Id,
    string Rank,
    string Number,
    string Company,
    [property: BsonRepresentation(BsonType.ObjectId)]
    string? LatestDescription
) {
    public TrainListing() : this(null, "", "", "", null) { }
    public TrainListing(string rank, string number, string company) : this(null, rank, number, company, null) { }
}