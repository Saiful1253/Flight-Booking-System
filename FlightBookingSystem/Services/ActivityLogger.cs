using FlightBookingSystem.Data;
using FlightBookingSystem.Models;

namespace FlightBookingSystem.Services;

/// <summary>Writes one row into the activity log (audit trail shown in Admin → Activity Logs).</summary>
public class ActivityLogger
{
    private readonly AppDbContext _db;

    public ActivityLogger(AppDbContext db) => _db = db;

    public async Task LogAsync(int? userId, string action, string entityType, int? entityId, string details)
    {
        _db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
