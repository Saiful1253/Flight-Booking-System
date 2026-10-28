using FlightBookingSystem.Models;
using Microsoft.AspNetCore.Identity;

namespace FlightBookingSystem.Data;

/// <summary>Creates the database (if missing) and fills it with demo data.</summary>
public static class DbInitializer
{
    public static void Initialize(AppDbContext db)
    {
        db.Database.EnsureCreated();

        if (db.Users.Any()) return;

        var hasher = new PasswordHasher<User>();
        string Hash(string pwd) => hasher.HashPassword(new User(), pwd);
        var now = DateTime.UtcNow;

        // ---------------- Users ----------------
        var admin = new User
        {
            FullName = "System Administrator",
            Email = "admin@flylocal.local",
            PasswordHash = Hash("Admin@123"),
            Phone = "01700000001",
            Role = UserRole.Admin
        };
        var clients = new[]
        {
            new User { FullName = "Rahim Uddin",  Email = "client@flylocal.local", PasswordHash = Hash("Client@123"), Phone = "01811111111", Role = UserRole.Client },
            new User { FullName = "Sumaiya Aktar", Email = "sumaiya@flylocal.local", PasswordHash = Hash("Client@123"), Phone = "01822222222", Role = UserRole.Client }
        };
        db.Users.Add(admin);
        db.Users.AddRange(clients);
        db.SaveChanges();

        // ---------------- Airlines ----------------
        var airlines = new[]
        {
            new Airline { Name = "Biman Bangladesh Airlines", Code = "BG", Color = "#1e4d8c", ContactEmail = "info@biman.gov.bd" },
            new Airline { Name = "US-Bangla Airlines",        Code = "BS", Color = "#c8102e", ContactEmail = "callcenter@usbanglaairlines.com" },
            new Airline { Name = "Novoair",                   Code = "VQ", Color = "#0f766e", ContactEmail = "info@novoairbd.com" },
            new Airline { Name = "Air Astra",                 Code = "2A", Color = "#7c3aed", ContactEmail = "support@airastra.com" },
            new Airline { Name = "Emirates",                  Code = "EK", Color = "#d71921", ContactEmail = "support@emirates.com" }
        };
        db.Airlines.AddRange(airlines);
        db.SaveChanges();

        // ---------------- Airports ----------------
        var airports = new[]
        {
            new Airport { Code = "DAC", Name = "Hazrat Shahjalal International Airport", City = "Dhaka",       Country = "Bangladesh", Currency = "BDT" },
            new Airport { Code = "CGP", Name = "Shah Amanat International Airport",      City = "Chattogram",  Country = "Bangladesh", Currency = "BDT" },
            new Airport { Code = "ZYL", Name = "Osmani International Airport",           City = "Sylhet",      Country = "Bangladesh", Currency = "BDT" },
            new Airport { Code = "CXB", Name = "Cox's Bazar Airport",                    City = "Cox's Bazar", Country = "Bangladesh", Currency = "BDT" },
            new Airport { Code = "DXB", Name = "Dubai International Airport",            City = "Dubai",       Country = "UAE",        Currency = "AED" },
            new Airport { Code = "BKK", Name = "Suvarnabhumi Airport",                   City = "Bangkok",     Country = "Thailand",   Currency = "THB" },
            new Airport { Code = "KUL", Name = "Kuala Lumpur International Airport",     City = "Kuala Lumpur",Country = "Malaysia",   Currency = "MYR" }
        };
        db.Airports.AddRange(airports);
        db.SaveChanges();

        // ---------------- Flights (next days) ----------------
        var flights = new List<Flight>
        {
            Domestic(airlines[0], airports[0], airports[1], 0, "08:15", 55, "Boeing 787-8",   5200, 9800, 0),
            Domestic(airlines[1], airports[1], airports[0], 0, "17:40", 55, "ATR 72-600",     4800, 0,   0),
            Domestic(airlines[2], airports[0], airports[2], 0, "12:30", 60, "Dash 8-Q400",    5600, 9200, 0),
            Domestic(airlines[3], airports[2], airports[0], 1, "09:00", 60, "ATR 72-600",     5400, 0,   0),
            Domestic(airlines[1], airports[0], airports[3], 1, "11:20", 50, "Boeing 737-800", 6900, 11500, 15800),
            Domestic(airlines[0], airports[3], airports[0], 2, "18:10", 50, "Boeing 737-800", 6700, 11000, 15000),
            International(airlines[4], airports[0], airports[4], 0, "03:45", 4*60+20, "Boeing 777-300ER", 34500, 78000, 132000),
            International(airlines[0], airports[0], airports[5], 1, "13:05", 2*60+35, "Boeing 787-9",     27800, 61000, 98000),
            International(airlines[1], airports[0], airports[6], 2, "20:30", 2*60+45, "Boeing 737-800",   24900, 52000, 0),
            International(airlines[4], airports[4], airports[0], 3, "22:15", 4*60+30, "Airbus A380-800", 36200, 81000, 138000)
        };
        db.Flights.AddRange(flights);
        db.SaveChanges();

        Flight Domestic(Airline al, Airport from, Airport to, int daysAhead, string hhmm, int mins,
                        string aircraft, decimal eco, decimal bus, decimal first)
        {
            var dep = now.Date.AddDays(daysAhead + 1).AddMinutes(ParseMinutes(hhmm)); // always in the future
            return Build(al, from, to, dep, mins, aircraft, eco, bus, first);
        }

        Flight International(Airline al, Airport from, Airport to, int daysAhead, string hhmm, int mins,
                             string aircraft, decimal eco, decimal bus, decimal first)
        {
            var dep = now.Date.AddDays(daysAhead + 1).AddMinutes(ParseMinutes(hhmm)); // always in the future
            return Build(al, from, to, dep, mins, aircraft, eco, bus, first);
        }

        static int ParseMinutes(string hhmm)
        {
            var parts = hhmm.Split(':');
            return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
        }

        static Flight Build(Airline al, Airport from, Airport to, DateTime dep, int mins,
                            string aircraft, decimal eco, decimal bus, decimal first)
        {
            return new Flight
            {
                FlightNumber = $"{al.Code}-{Random.Shared.Next(100, 999)}",
                AirlineId = al.Id,
                DepartureAirportId = from.Id,
                ArrivalAirportId = to.Id,
                DepartureAt = dep,
                ArrivalAt = dep.AddMinutes(mins),
                DurationMinutes = mins,
                AircraftType = aircraft,
                Status = FlightStatus.Scheduled,
                EconomyTotal = 150, EconomyAvailable = 150, EconomyPrice = eco,
                BusinessTotal = bus > 0 ? 20 : 0, BusinessAvailable = bus > 0 ? 20 : 0, BusinessPrice = bus,
                FirstTotal = first > 0 ? 8 : 0, FirstAvailable = first > 0 ? 8 : 0, FirstPrice = first
            };
        }

        // ---------------- Bookings ----------------
        var f0 = flights[0]; // tomorrow DAC -> CGP
        var f6 = flights[6]; // tonight DAC -> DXB

        var b1 = new Booking
        {
            Pnr = "BGH47K", UserId = clients[0].Id, FlightId = f0.Id, CabinClass = CabinClass.Economy,
            PassengerCount = 2, TotalPrice = f0.EconomyPrice * 2, Status = BookingStatus.Confirmed,
            ContactEmail = clients[0].Email, ContactPhone = clients[0].Phone,
            PaymentRef = "PAY-88412037", CardLast4 = "4242",
            VerifiedById = admin.Id, VerifiedAt = now.AddHours(-2),
            CreatedAt = now.AddHours(-26), UpdatedAt = now.AddHours(-2)
        };
        var b2 = new Booking // duplicate: same user, same flight
        {
            Pnr = "DUP99X", UserId = clients[0].Id, FlightId = f0.Id, CabinClass = CabinClass.Economy,
            PassengerCount = 1, TotalPrice = f0.EconomyPrice, Status = BookingStatus.Pending,
            ContactEmail = clients[0].Email, ContactPhone = clients[0].Phone,
            PaymentRef = "PAY-8841999", CardLast4 = "4242",
            CreatedAt = now.AddHours(-3), UpdatedAt = now.AddHours(-3)
        };
        b2.DuplicateOfId = null; // set after SaveChanges (FK)

        var b3 = new Booking
        {
            Pnr = "K7QX2M", UserId = clients[1].Id, FlightId = f6.Id, CabinClass = CabinClass.Economy,
            PassengerCount = 1, TotalPrice = f6.EconomyPrice, Status = BookingStatus.Pending,
            ContactEmail = clients[1].Email, ContactPhone = clients[1].Phone,
            PaymentRef = "PAY-8842011", CardLast4 = "1881",
            CreatedAt = now.AddHours(-5), UpdatedAt = now.AddHours(-5)
        };

        var b4 = new Booking
        {
            Pnr = "CNL33A", UserId = clients[1].Id, FlightId = flights[7].Id, CabinClass = CabinClass.Economy,
            PassengerCount = 1, TotalPrice = flights[7].EconomyPrice, Status = BookingStatus.Cancelled,
            CreatedAt = now.AddDays(-6), UpdatedAt = now.AddDays(-4)
        };

        db.Bookings.AddRange(b1, b2, b3, b4);
        db.SaveChanges();

        b2.DuplicateOfId = b1.Id;

        db.Passengers.AddRange(
            P(b1, f0.Id, "Rahim Uddin",      "3A", 34, Gender.Male,   IdType.NID, "1987654321"),
            P(b1, f0.Id, "Jharna Rahim",     "3B", 30, Gender.Female, IdType.NID, "1987654322"),
            P(b2, f0.Id, "Rahim Uddin",      "4A", 34, Gender.Male,   IdType.NID, "1987654321"),
            P(b3, f6.Id, "Sumaiya Aktar",    "5A", 28, Gender.Female, IdType.Passport, "EF4455667"),
            P(b4, flights[7].Id, "Sumaiya Aktar", null, 28, Gender.Female, IdType.Passport, "EF4455667")
        );

        // Seat inventory reflects the sold seats
        f0.EconomyAvailable -= 3;
        f6.EconomyAvailable -= 1;

        db.SaveChanges();

        static Passenger P(Booking b, int flightId, string name, string? seat, int age, Gender g, IdType idType, string idNo) => new()
        {
            BookingId = b.Id, FlightId = flightId, FullName = name, SeatNumber = seat,
            Age = age, Gender = g, IdType = idType, IdNumber = idNo
        };

        // ---------------- Activity logs ----------------
        db.ActivityLogs.AddRange(
            Log(clients[0].Id, "Registration", "User", clients[0].Id, $"{clients[0].FullName} created an account", now.AddDays(-30)),
            Log(clients[0].Id, "Booking Created", "Booking", b1.Id, $"Booking #{b1.Id} (PNR {b1.Pnr}) created for flight {f0.FlightNumber}", b1.CreatedAt),
            Log(admin.Id, "Booking Verified", "Booking", b1.Id, $"Booking #{b1.Id} (PNR {b1.Pnr}) verified and confirmed by admin", b1.VerifiedAt!.Value),
            Log(clients[0].Id, "Booking Created", "Booking", b2.Id, $"Booking #{b2.Id} (PNR {b2.Pnr}) created for flight {f0.FlightNumber}", b2.CreatedAt),
            Log(clients[0].Id, "Duplicate Flagged", "Booking", b2.Id, $"Auto-duplicate detection matched this booking with #{b1.Id} (same user and flight)", b2.CreatedAt),
            Log(clients[1].Id, "Booking Created", "Booking", b3.Id, $"Booking #{b3.Id} (PNR {b3.Pnr}) created for flight {f6.FlightNumber}", b3.CreatedAt),
            Log(clients[1].Id, "Booking Cancelled", "Booking", b4.Id, $"Booking #{b4.Id} (PNR {b4.Pnr}) cancelled by client", b4.UpdatedAt)
        );
        db.SaveChanges();

        static ActivityLog Log(int? userId, string action, string type, int entityId, string details, DateTime at) =>
            new() { UserId = userId, Action = action, EntityType = type, EntityId = entityId, Details = details, CreatedAt = at };
    }
}
