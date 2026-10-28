using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightBookingSystem.Models;

public class Booking
{
    public int Id { get; set; }

    [Required, MaxLength(8)]
    public string Pnr { get; set; } = string.Empty;   // e.g. K7QX2M

    public int UserId { get; set; }
    public User? User { get; set; }

    public int FlightId { get; set; }
    public Flight? Flight { get; set; }

    public CabinClass CabinClass { get; set; } = CabinClass.Economy;

    public int PassengerCount { get; set; } = 1;

    public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    // ---- Contact for this trip ----
    [MaxLength(150)]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    // ---- Mock payment info (no real gateway) ----
    [MaxLength(40)]
    public string? PaymentRef { get; set; }

    [MaxLength(4)]
    public string? CardLast4 { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public int? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Set automatically when the same user books the same flight twice.</summary>
    public int? DuplicateOfId { get; set; }
    public Booking? DuplicateOf { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Passenger> Passengers { get; set; } = new List<Passenger>();

    [NotMapped]
    public bool IsActive => Status is BookingStatus.Pending or BookingStatus.Confirmed;
}

public class Passenger
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Needed for the (flight, seat) unique index that prevents double seat sales.</summary>
    public int FlightId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public IdType IdType { get; set; } = IdType.NID;

    [Required, MaxLength(40)]
    public string IdNumber { get; set; } = string.Empty;

    public int Age { get; set; }

    public Gender Gender { get; set; } = Gender.Male;

    [MaxLength(5)]
    public string? SeatNumber { get; set; }   // 12A — cleared when the booking is cancelled
}
