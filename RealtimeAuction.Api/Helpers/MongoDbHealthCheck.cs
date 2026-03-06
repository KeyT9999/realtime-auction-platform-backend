using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace RealtimeAuction.Api.Helpers;

/// <summary>
/// Custom health check for MongoDB connectivity.
/// Pings the database and reports Healthy/Unhealthy.
/// </summary>
public class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoDbHealthCheck(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Run { ping: 1 } command against MongoDB
            var command = new BsonDocument("ping", 1);
            await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "MongoDB is unreachable.",
                exception: ex);
        }
    }
}
