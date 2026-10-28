using System.Text.Json;
using FlightBookingSystem.Data;
using FlightBookingSystem.Models;
using FlightBookingSystem.Services;
using FlightBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightBookingSystem.Controllers;

[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly ActivityLogger _logger;

    public AdminController(AppDbContext db, ActivityLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ---------------- Dashboard ----------------
    public async Task<IActionResult> Index()
    {
        var bookings = await _db.Bookings
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.User)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var vm = new AdminDashboardViewModel
        {
            TotalBookings = bookings.Count,
            PendingApprovals = bookings.Count(b => b.Status == BookingStatus.Pending),
            ConfirmedBookings = bookings.Count(b => b.Status == BookingStatus.Confirmed),
            ActiveFlights = await _db.Flights.CountAsync(f => f.Status == FlightStatus.Scheduled && f.DepartureAt > now),
            TotalUsers = await _db.Users.CountAsync(u => u.Role == UserRole.Client),
            SeatsSold = bookings.Where(b => b.IsActive).Sum(b => b.PassengerCount),
            Revenue = bookings.Where(b => b.Status == BookingStatus.Confirmed).Sum(b => b.TotalPrice),
            DuplicateFlags = bookings.Count(b => b.DuplicateOfId != null && b.IsActive),
            // Priority queue: departure soonest first
            PendingQueue = bookings.Where(b => b.Status == BookingStatus.Pending)
                .OrderBy(b => b.Flight!.DepartureAt).Take(6).ToList(),
            RecentBookings = bookings.OrderByDescending(b => b.CreatedAt).Take(7).ToList(),
            DepartingSoon = await _db.Flights
                .Include(f => f.Airline).Include(f => f.DepartureAirport).Include(f => f.ArrivalAirport)
                .Where(f => f.DepartureAt > now && f.Status == FlightStatus.Scheduled)
                .OrderBy(f => f.DepartureAt).Take(6).ToListAsync()
        };

        var byMonth = Enumerable.Range(0, 6).Select(i =>
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(5 - i));
            var end = start.AddMonths(1);
            var inMonth = bookings.Where(b => b.CreatedAt >= start && b.CreatedAt < end).ToList();
            return new { Label = start.ToString("MMM"), Count = inMonth.Count,
                         Revenue = inMonth.Where(b => b.Status == BookingStatus.Confirmed).Sum(b => b.TotalPrice) };
        }).ToList();

        ViewBag.ChartLabels = JsonSerializer.Serialize(byMonth.Select(m => m.Label));
        ViewBag.ChartCounts = JsonSerializer.Serialize(byMonth.Select(m => m.Count));
        ViewBag.RevenueLabels = JsonSerializer.Serialize(byMonth.Select(m => m.Label));
        ViewBag.RevenueValues = JsonSerializer.Serialize(byMonth.Select(m => (double)m.Revenue));

        return View(vm);
    }

    // ---------------- Bookings ----------------
    public async Task<IActionResult> Bookings(string? status, string? cls, string? q)
    {
        var query = _db.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.DuplicateOf)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, out var s))
            query = query.Where(b => b.Status == s);
        if (!string.IsNullOrWhiteSpace(cls) && Enum.TryParse<CabinClass>(cls, out var c))
            query = query.Where(b => b.CabinClass == c);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(b => b.Pnr.Contains(q) || b.User!.FullName.Contains(q) ||
                                    b.Flight!.FlightNumber.Contains(q));

        ViewBag.StatusFilter = status;
        ViewBag.ClassFilter = cls;
        ViewBag.Query = q;

        // Priority order: departing soonest first
        var list = await query.OrderByDescending(b => b.Flight!.DepartureAt).ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> BookingDetails(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
            .Include(b => b.DuplicateOf)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();

        ViewBag.Logs = await _db.ActivityLogs.Include(l => l.User)
            .Where(l => l.EntityType == "Booking" && l.EntityId == id)
            .OrderByDescending(l => l.CreatedAt).ToListAsync();

        return View(booking);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Only pending bookings can be approved.";
            return RedirectToAction(nameof(BookingDetails), new { id });
        }

        var now = DateTime.UtcNow;
        booking.Status = BookingStatus.Confirmed;
        booking.VerifiedById = User.GetUserId();
        booking.VerifiedAt = now;
        booking.UpdatedAt = now;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Booking Verified", "Booking", id,
            $"Booking #{id} (PNR {booking.Pnr}) verified and confirmed by admin");

        TempData["Success"] = $"Booking {booking.Pnr} confirmed. Seats are now locked for the passengers.";
        return RedirectToAction(nameof(BookingDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        var booking = await _db.Bookings
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.Pending)
        {
            TempData["Error"] = "Only pending bookings can be rejected.";
            return RedirectToAction(nameof(BookingDetails), new { id });
        }

        BookingController.ReleaseBooking(booking); // frees seats, sets Cancelled...
        booking.Status = BookingStatus.Rejected;               // ...then we mark it rejected
        booking.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Payment could not be verified" : reason.Trim();
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Booking Rejected", "Booking", id,
            $"Booking #{id} (PNR {booking.Pnr}) rejected: {booking.RejectionReason}");

        TempData["Success"] = $"Booking {booking.Pnr} rejected and seats released.";
        return RedirectToAction(nameof(BookingDetails), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        if (!booking.IsActive)
        {
            TempData["Error"] = "This booking is already closed.";
            return RedirectToAction(nameof(BookingDetails), new { id });
        }

        BookingController.ReleaseBooking(booking);
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Booking Cancelled", "Booking", id,
            $"Booking #{id} (PNR {booking.Pnr}) cancelled by admin");

        TempData["Success"] = $"Booking {booking.Pnr} cancelled and seats released.";
        return RedirectToAction(nameof(BookingDetails), new { id });
    }

    // ---------------- Duplicates ----------------
    public async Task<IActionResult> Duplicates()
    {
        var flagged = await DuplicateDetector.GetFlaggedAsync(_db);
        return View(flagged);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearDuplicate(int id)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking is null) return NotFound();

        booking.DuplicateOfId = null;
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Duplicate Dismissed", "Booking", id,
            $"Admin dismissed the duplicate flag on booking #{id}");

        TempData["Success"] = "Duplicate flag cleared — both bookings stay active.";
        return RedirectToAction(nameof(Duplicates));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MergeDuplicate(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        var originalId = booking.DuplicateOfId;
        BookingController.ReleaseBooking(booking);
        booking.DuplicateOfId = originalId;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Duplicate Merged", "Booking", id,
            $"Duplicate booking #{id} merged into #{originalId} and closed");

        TempData["Success"] = $"Booking {booking.Pnr} merged into #{originalId} — seats released.";
        return RedirectToAction(nameof(Duplicates));
    }

    // ---------------- Flights (resource inventory) ----------------
    public async Task<IActionResult> Flights(string? q, string? status)
    {
        var query = _db.Flights
            .Include(f => f.Airline)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(f => f.FlightNumber.Contains(q) ||
                                     f.DepartureAirport!.City.Contains(q) ||
                                     f.ArrivalAirport!.City.Contains(q) ||
                                     f.Airline!.Name.Contains(q));
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<FlightStatus>(status, out var s))
            query = query.Where(f => f.Status == s);

        ViewBag.Query = q;
        ViewBag.StatusFilter = status;
        var list = await query.OrderByDescending(f => f.DepartureAt).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> FlightCreate()
    {
        await FillSelectsAsync();
        return View(new Flight { DepartureAt = DateTime.UtcNow.AddDays(1).Date.AddHours(9) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FlightCreate(Flight vm)
    {
        await FillSelectsAsync();
        if (vm.ArrivalAirportId == vm.DepartureAirportId)
            ModelState.AddModelError(nameof(vm.ArrivalAirportId), "Origin and destination must be different.");
        if (vm.DurationMinutes <= 0)
            ModelState.AddModelError(nameof(vm.DurationMinutes), "Duration must be greater than zero.");
        if (vm.DepartureAt <= DateTime.UtcNow)
            ModelState.AddModelError(nameof(vm.DepartureAt), "Departure must be in the future.");
        vm.DepartureAt = DateTime.SpecifyKind(vm.DepartureAt, DateTimeKind.Utc);
        if (!ModelState.IsValid) return View(vm);

        vm.ArrivalAt = vm.DepartureAt.AddMinutes(vm.DurationMinutes);
        vm.EconomyAvailable = Math.Clamp(vm.EconomyAvailable, 0, vm.EconomyTotal);
        vm.BusinessAvailable = Math.Clamp(vm.BusinessAvailable, 0, vm.BusinessTotal);
        vm.FirstAvailable = Math.Clamp(vm.FirstAvailable, 0, vm.FirstTotal);
        vm.CreatedAt = vm.UpdatedAt = DateTime.UtcNow;
        vm.Status = FlightStatus.Scheduled;

        _db.Flights.Add(vm);
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Flight Created", "Flight", vm.Id,
            $"Flight {vm.FlightNumber} ({DepartCode(vm)} -> {ArriveCode(vm)}) added to inventory");

        TempData["Success"] = $"Flight {vm.FlightNumber} created.";
        return RedirectToAction(nameof(Flights));
    }

    [HttpGet]
    public async Task<IActionResult> FlightEdit(int id)
    {
        var flight = await _db.Flights.FindAsync(id);
        if (flight is null) return NotFound();
        await FillSelectsAsync();
        return View(flight);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FlightEdit(int id, Flight vm)
    {
        var flight = await _db.Flights.FindAsync(id);
        if (flight is null) return NotFound();

        await FillSelectsAsync();
        if (vm.ArrivalAirportId == vm.DepartureAirportId)
            ModelState.AddModelError(nameof(vm.ArrivalAirportId), "Origin and destination must be different.");
        if (vm.DurationMinutes <= 0)
            ModelState.AddModelError(nameof(vm.DurationMinutes), "Duration must be greater than zero.");
        vm.DepartureAt = DateTime.SpecifyKind(vm.DepartureAt, DateTimeKind.Utc);
        if (!ModelState.IsValid) return View(vm);

        // Keep available seats consistent when the total changes
        flight.EconomyAvailable = Math.Clamp(flight.EconomyAvailable + (vm.EconomyTotal - flight.EconomyTotal), 0, vm.EconomyTotal);
        flight.BusinessAvailable = Math.Clamp(flight.BusinessAvailable + (vm.BusinessTotal - flight.BusinessTotal), 0, vm.BusinessTotal);
        flight.FirstAvailable = Math.Clamp(flight.FirstAvailable + (vm.FirstTotal - flight.FirstTotal), 0, vm.FirstTotal);

        flight.FlightNumber = vm.FlightNumber.Trim().ToUpperInvariant();
        flight.AirlineId = vm.AirlineId;
        flight.DepartureAirportId = vm.DepartureAirportId;
        flight.ArrivalAirportId = vm.ArrivalAirportId;
        flight.DepartureAt = vm.DepartureAt;
        flight.DurationMinutes = vm.DurationMinutes;
        flight.ArrivalAt = vm.DepartureAt.AddMinutes(vm.DurationMinutes);
        flight.AircraftType = vm.AircraftType;
        flight.Status = vm.Status;
        flight.EconomyTotal = Math.Max(1, vm.EconomyTotal);
        flight.BusinessTotal = Math.Max(0, vm.BusinessTotal);
        flight.FirstTotal = Math.Max(0, vm.FirstTotal);
        flight.EconomyPrice = vm.EconomyPrice;
        flight.BusinessPrice = vm.BusinessPrice;
        flight.FirstPrice = vm.FirstPrice;
        flight.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Flight Updated", "Flight", id,
            $"Flight {flight.FlightNumber} updated (status: {flight.Status})");

        TempData["Success"] = $"Flight {flight.FlightNumber} updated.";
        return RedirectToAction(nameof(Flights));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FlightDelete(int id)
    {
        var flight = await _db.Flights.FindAsync(id);
        if (flight is null) return NotFound();

        if (await _db.Bookings.AnyAsync(b => b.FlightId == id))
        {
            TempData["Error"] = "This flight has bookings and cannot be deleted. Mark it Cancelled instead.";
            return RedirectToAction(nameof(Flights));
        }

        _db.Flights.Remove(flight);
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "Flight Deleted", "Flight", id,
            $"Flight {flight.FlightNumber} removed from inventory");

        TempData["Success"] = "Flight deleted.";
        return RedirectToAction(nameof(Flights));
    }

    // ---------------- Airlines ----------------
    public async Task<IActionResult> Airlines() =>
        View(await _db.Airlines.OrderBy(a => a.Name).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AirlineSave(Airline vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Airline name and code are required.";
            return RedirectToAction(nameof(Airlines));
        }

        vm.Code = vm.Code.Trim().ToUpperInvariant();
        if (_db.Airlines.Any(a => a.Code == vm.Code && a.Id != vm.Id))
        {
            TempData["Error"] = $"Airline code {vm.Code} already exists.";
            return RedirectToAction(nameof(Airlines));
        }

        if (vm.Id == 0)
        {
            _db.Airlines.Add(vm);
            await _db.SaveChangesAsync();
            await _logger.LogAsync(User.GetUserId(), "Airline Added", "Airline", vm.Id, $"{vm.Name} ({vm.Code}) added");
            TempData["Success"] = "Airline added.";
        }
        else
        {
            var airline = await _db.Airlines.FindAsync(vm.Id);
            if (airline is null) return RedirectToAction(nameof(Airlines));
            airline.Name = vm.Name;
            airline.Code = vm.Code;
            airline.Color = vm.Color;
            airline.ContactEmail = vm.ContactEmail;
            await _db.SaveChangesAsync();
            await _logger.LogAsync(User.GetUserId(), "Airline Updated", "Airline", vm.Id, $"{vm.Name} updated");
            TempData["Success"] = "Airline updated.";
        }
        return RedirectToAction(nameof(Airlines));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AirlineDelete(int id)
    {
        var airline = await _db.Airlines.FindAsync(id);
        if (airline is null) return NotFound();

        if (await _db.Flights.AnyAsync(f => f.AirlineId == id))
        {
            TempData["Error"] = "Airline has flights and cannot be deleted.";
            return RedirectToAction(nameof(Airlines));
        }

        _db.Airlines.Remove(airline);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Airline removed.";
        return RedirectToAction(nameof(Airlines));
    }

    // ---------------- Airports ----------------
    public async Task<IActionResult> Airports() =>
        View(await _db.Airports.OrderBy(a => a.City).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AirportSave(Airport vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Code, name, city and country are required.";
            return RedirectToAction(nameof(Airports));
        }

        vm.Code = vm.Code.Trim().ToUpperInvariant();
        if (_db.Airports.Any(a => a.Code == vm.Code && a.Id != vm.Id))
        {
            TempData["Error"] = $"Airport code {vm.Code} already exists.";
            return RedirectToAction(nameof(Airports));
        }

        if (vm.Id == 0)
        {
            _db.Airports.Add(vm);
            await _db.SaveChangesAsync();
            await _logger.LogAsync(User.GetUserId(), "Airport Added", "Airport", vm.Id, $"{vm.Code} - {vm.City} added");
            TempData["Success"] = "Airport added.";
        }
        else
        {
            var airport = await _db.Airports.FindAsync(vm.Id);
            if (airport is null) return RedirectToAction(nameof(Airports));
            airport.Code = vm.Code;
            airport.Name = vm.Name;
            airport.City = vm.City;
            airport.Country = vm.Country;
            airport.Currency = vm.Currency;
            await _db.SaveChangesAsync();
            await _logger.LogAsync(User.GetUserId(), "Airport Updated", "Airport", vm.Id, $"{vm.Code} updated");
            TempData["Success"] = "Airport updated.";
        }
        return RedirectToAction(nameof(Airports));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AirportDelete(int id)
    {
        var airport = await _db.Airports.FindAsync(id);
        if (airport is null) return NotFound();

        if (await _db.Flights.AnyAsync(f => f.DepartureAirportId == id || f.ArrivalAirportId == id))
        {
            TempData["Error"] = "Airport has flights and cannot be deleted.";
            return RedirectToAction(nameof(Airports));
        }

        _db.Airports.Remove(airport);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Airport removed.";
        return RedirectToAction(nameof(Airports));
    }

    // ---------------- Users ----------------
    public async Task<IActionResult> Users()
    {
        var users = await _db.Users
            .Select(u => new { u, Count = u.Bookings.Count })
            .OrderByDescending(x => x.u.Role).ThenBy(x => x.u.FullName)
            .ToListAsync();

        ViewBag.Counts = users.ToDictionary(x => x.u.Id, x => x.Count);
        return View(users.Select(x => x.u).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null || user.Id == User.GetUserId())
        {
            TempData["Error"] = "You cannot change your own account status.";
            return RedirectToAction(nameof(Users));
        }

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "User Status Changed", "User", id,
            $"{user.FullName} is now {(user.IsActive ? "active" : "inactive")}");

        TempData["Success"] = $"{user.FullName} is now {(user.IsActive ? "active" : "inactive")}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRole(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null || user.Id == User.GetUserId())
        {
            TempData["Error"] = "You cannot change your own role.";
            return RedirectToAction(nameof(Users));
        }

        user.Role = user.Role == UserRole.Admin ? UserRole.Client : UserRole.Admin;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(User.GetUserId(), "User Role Changed", "User", id,
            $"{user.FullName} role changed to {user.Role}");

        TempData["Success"] = $"{user.FullName} is now {user.Role}.";
        return RedirectToAction(nameof(Users));
    }

    // ---------------- Reports ----------------
    public async Task<IActionResult> Reports(int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);

        var bookings = await _db.Bookings
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Where(b => b.CreatedAt >= since)
            .ToListAsync();

        var flights = await _db.Flights
            .Where(f => f.DepartureAt >= since)
            .ToListAsync();

        var confirmed = bookings.Where(b => b.Status == BookingStatus.Confirmed).ToList();

        var vm = new ReportsViewModel
        {
            Days = days,
            TotalBookings = bookings.Count,
            Confirmed = confirmed.Count,
            Cancelled = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            Rejected = bookings.Count(b => b.Status == BookingStatus.Rejected),
            SeatsSold = confirmed.Sum(b => b.PassengerCount),
            Revenue = confirmed.Sum(b => b.TotalPrice),
            AvgOccupancy = flights.Count == 0 ? 0 : Math.Round(flights.Average(f => f.OccupancyPercent), 1),
            ByAirline = confirmed.GroupBy(b => b.Flight!.Airline!.Name)
                .Select(g => (g.Key, g.Count(), g.Sum(b => b.TotalPrice)))
                .OrderByDescending(x => x.Item3).ToList(),
            ByClass = bookings.GroupBy(b => b.CabinClass)
                .Select(g => (g.Key.ToString(), g.Count()))
                .OrderByDescending(x => x.Item2).ToList(),
            ByRoute = confirmed.GroupBy(b => $"{b.Flight!.DepartureAirport!.Code} - {b.Flight.ArrivalAirport!.Code}")
                .Select(g => (g.Key, g.Count()))
                .OrderByDescending(x => x.Item2).Take(8).ToList(),
            RecentActivity = await _db.ActivityLogs.Include(l => l.User)
                .OrderByDescending(l => l.CreatedAt).Take(12).ToListAsync()
        };
        return View(vm);
    }

    // ---------------- Activity logs ----------------
    public async Task<IActionResult> Logs(string? q)
    {
        var query = _db.ActivityLogs.Include(l => l.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(l => l.Action.Contains(q) || l.Details.Contains(q));

        ViewBag.Query = q;
        var list = await query.OrderByDescending(l => l.CreatedAt).Take(300).ToListAsync();
        return View(list);
    }

    // ---------------- Helpers ----------------
    private async Task FillSelectsAsync()
    {
        ViewBag.Airlines = await _db.Airlines.OrderBy(a => a.Name).ToListAsync();
        ViewBag.Airports = await _db.Airports.OrderBy(a => a.City).ToListAsync();
    }

    private static string DepartCode(Flight f) => f.DepartureAirportId.ToString();
    private static string ArriveCode(Flight f) => f.ArrivalAirportId.ToString();
}
