using System.ComponentModel.DataAnnotations;

namespace FlightBookingSystem.Models;

public class Airport
{
    public int Id { get; set; }

    [Required, MaxLength(5)]
    public string Code { get; set; } = string.Empty;   // DAC, CGP, DXB...

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Country { get; set; } = string.Empty;

    [MaxLength(3)]
    public string Currency { get; set; } = "BDT";
}

public class Airline
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(3)]
    public string Code { get; set; } = string.Empty;   // BG, BS, EK...

    /// <summary>Brand colour used across the UI instead of a logo image.</summary>
    [Required, MaxLength(7)]
    public string Color { get; set; } = "#0284c7";

    [MaxLength(150)]
    public string? ContactEmail { get; set; }
}
