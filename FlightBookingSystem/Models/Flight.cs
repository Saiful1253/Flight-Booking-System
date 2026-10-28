using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlightBookingSystem.Models;

public class Flight
{
    public int Id { get; set; }

    [Required, MaxLength(15)]
    public string FlightNumber { get; set; } = string.Empty;   // BG-147

    public int AirlineId { get; set; }
    public Airline? Airline { get; set; }

    public int DepartureAirportId { get; set; }
    public Airport? DepartureAirport { get; set; }

    public int ArrivalAirportId { get; set; }
    public Airport? ArrivalAirport { get; set; }

    public DateTime DepartureAt { get; set; }
    public DateTime ArrivalAt { get; set; }

    public int DurationMinutes { get; set; }

    [MaxLength(60)]
    public string? AircraftType { get; set; }

    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    // ---- Seats & prices per cabin class ----
    public int EconomyTotal { get; set; } = 150;
    public int EconomyAvailable { get; set; } = 150;
    public decimal EconomyPrice { get; set; }

    public int BusinessTotal { get; set; } = 20;
    public int BusinessAvailable { get; set; } = 20;
    public decimal BusinessPrice { get; set; }

    public int FirstTotal { get; set; } = 8;
    public int FirstAvailable { get; set; } = 8;
    public decimal FirstPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsDomestic => string.Equals(
        DepartureAirport?.Country, ArrivalAirport?.Country, StringComparison.OrdinalIgnoreCase);

    [NotMapped]
    public int TotalSeats => EconomyTotal + BusinessTotal + FirstTotal;

    [NotMapped]
    public int TotalAvailable => EconomyAvailable + BusinessAvailable + FirstAvailable;

    [NotMapped]
    public double OccupancyPercent => TotalSeats == 0 ? 0
        : Math.Round((TotalSeats - TotalAvailable) * 100.0 / TotalSeats, 1);
}
