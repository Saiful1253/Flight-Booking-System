using System.ComponentModel.DataAnnotations;

namespace FlightBookingSystem.Models;

public class ActivityLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(120)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Booking, Flight, User, Airport, Airline...</summary>
    [Required, MaxLength(60)]
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    [MaxLength(1000)]
    public string Details { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
