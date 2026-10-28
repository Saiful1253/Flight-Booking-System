using System.ComponentModel.DataAnnotations;
using FlightBookingSystem.Models;

namespace FlightBookingSystem.ViewModels;

// ---------------- Search ----------------
public class LoginViewModel
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterViewModel
{
    [Required, MaxLength(100), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Phone, MaxLength(20), Display(Name = "Phone number")]
    public string? Phone { get; set; }

    [Required, DataType(DataType.Password), MinLength(6), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Client;
}

public class FlightSearchViewModel
{
    [Required, Display(Name = "From")]
    public int FromAirportId { get; set; }

    [Required, Display(Name = "To")]
    public int ToAirportId { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Departure date")]
    public DateTime Date { get; set; } = DateTime.Today.AddDays(1);

    [Range(1, 9), Display(Name = "Passengers")]
    public int Passengers { get; set; } = 1;

    [Display(Name = "Cabin class")]
    public CabinClass CabinClass { get; set; } = CabinClass.Economy;

    public List<Airport> Airports { get; set; } = new();
}

public class FlightResultItem
{
    public Flight Flight { get; set; } = new();
    public decimal UnitPrice { get; set; }
    public int SeatsLeft { get; set; }
    public bool Available { get; set; }
}

// ---------------- Booking form ----------------
public class PassengerInput
{
    [Required, MaxLength(100), Display(Name = "Full name (as on ID)")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "ID type")]
    public IdType IdType { get; set; } = IdType.NID;

    [Required, MaxLength(40), Display(Name = "ID number")]
    public string IdNumber { get; set; } = string.Empty;

    [Range(1, 120), Display(Name = "Age")]
    public int Age { get; set; } = 25;

    [Display(Name = "Gender")]
    public Gender Gender { get; set; } = Gender.Male;
}

public class BookViewModel
{
    public int FlightId { get; set; }
    public Flight? Flight { get; set; }
    public CabinClass CabinClass { get; set; }
    public int Passengers { get; set; } = 1;

    [Required, EmailAddress, MaxLength(150), Display(Name = "Contact email")]
    public string ContactEmail { get; set; } = string.Empty;

    [Required, Phone, MaxLength(20), Display(Name = "Contact phone")]
    public string ContactPhone { get; set; } = string.Empty;

    [Display(Name = "Seat preference (aisle/window letter)")]
    public string? SeatPreference { get; set; } = "A";

    [Required, MaxLength(100), Display(Name = "Name on card")]
    public string CardName { get; set; } = string.Empty;

    [Required, CreditCard, MaxLength(19), Display(Name = "Card number")]
    public string CardNumber { get; set; } = string.Empty;

    [Required, MaxLength(5), Display(Name = "Expiry (MM/YY)")]
    public string Expiry { get; set; } = string.Empty;

    [Required, MaxLength(4), Display(Name = "CVV")]
    public string Cvv { get; set; } = string.Empty;

    public List<PassengerInput> PassengerList { get; set; } = new();

    public decimal TotalPrice => (Flight is null ? 0 : UnitPrice) * Math.Max(1, Passengers);
    public decimal UnitPrice => CabinClass switch
    {
        CabinClass.Business => Flight?.BusinessPrice ?? 0,
        CabinClass.First => Flight?.FirstPrice ?? 0,
        _ => Flight?.EconomyPrice ?? 0
    };
}

// ---------------- Client dashboard ----------------
public class ClientDashboardViewModel
{
    public int TotalBookings { get; set; }
    public int UpcomingTrips { get; set; }
    public int CancelledCount { get; set; }
    public List<Booking> Upcoming { get; set; } = new();
    public List<Booking> Recent { get; set; } = new();
}

// ---------------- Admin ----------------
public class AdminDashboardViewModel
{
    public int TotalBookings { get; set; }
    public int PendingApprovals { get; set; }
    public int ConfirmedBookings { get; set; }
    public int ActiveFlights { get; set; }
    public int TotalUsers { get; set; }
    public int SeatsSold { get; set; }
    public decimal Revenue { get; set; }
    public int DuplicateFlags { get; set; }
    public List<Booking> PendingQueue { get; set; } = new();   // sorted: departure soonest = top priority
    public List<Booking> RecentBookings { get; set; } = new();
    public List<Flight> DepartingSoon { get; set; } = new();
}

public class ReportsViewModel
{
    public int Days { get; set; } = 30;
    public int TotalBookings { get; set; }
    public int Confirmed { get; set; }
    public int Cancelled { get; set; }
    public int Rejected { get; set; }
    public int SeatsSold { get; set; }
    public decimal Revenue { get; set; }
    public double AvgOccupancy { get; set; }
    public List<(string Label, int Count, decimal Revenue)> ByAirline { get; set; } = new();
    public List<(string Label, int Count)> ByClass { get; set; } = new();
    public List<(string Label, int Count)> ByRoute { get; set; } = new();
    public List<ActivityLog> RecentActivity { get; set; } = new();
}
