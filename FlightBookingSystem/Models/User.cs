using System.ComponentModel.DataAnnotations;

namespace FlightBookingSystem.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    public UserRole Role { get; set; } = UserRole.Client;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
