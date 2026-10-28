using FlightBookingSystem.Data;
using FlightBookingSystem.Models;
using FlightBookingSystem.Services;
using FlightBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightBookingSystem.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly AppDbContext _db;
    private readonly ActivityLogger _logger;

    public BookingController(AppDbContext db, ActivityLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ---------------- Booking form ----------------
    [HttpGet]
    public async Task<IActionResult> Book(int id, CabinClass cls = CabinClass.Economy, int pax = 1,
                                          int? from = null, int? to = null, DateTime? date = null)
    {
        var flight = await _db.Flights
            .Include(f => f.Airline)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (flight is null) return NotFound();

        var seatsLeft = SeatsLeft(flight, cls);
        if (flight.Status != FlightStatus.Scheduled || seatsLeft < pax || flight.DepartureAt <= DateTime.UtcNow)
        {
            TempData["Error"] = "This flight can no longer be booked (departed, cancelled or sold out).";
            return RedirectToAction("Index", "Home");
        }

        var user = await _db.Users.FindAsync(User.GetUserId());
        pax = Math.Clamp(pax, 1, Math.Min(9, seatsLeft));

        var vm = new BookViewModel
        {
            FlightId = flight.Id,
            Flight = flight,
            CabinClass = cls,
            Passengers = pax,
            ContactEmail = user?.Email ?? string.Empty,
            ContactPhone = user?.Phone,
            PassengerList = Enumerable.Range(0, pax).Select(_ => new PassengerInput
            {
                FullName = user?.FullName ?? string.Empty
            }).ToList()
        };

        ViewBag.From = from; ViewBag.To = to; ViewBag.Date = date;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookViewModel vm, int? from = null, int? to = null, DateTime? date = null)
    {
        var flight = await _db.Flights
            .Include(f => f.Airline)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefaultAsync(f => f.Id == vm.FlightId);

        if (flight is null) return NotFound();

        // Rebuild passenger list server-side (never trust the posted count)
        if (vm.PassengerList.Count != vm.Passengers)
        {
            while (vm.PassengerList.Count < vm.Passengers) vm.PassengerList.Add(new PassengerInput());
            vm.PassengerList = vm.PassengerList.Take(vm.Passengers).ToList();
        }

        var seatsLeft = SeatsLeft(flight, vm.CabinClass);
        if (flight.Status != FlightStatus.Scheduled || seatsLeft < vm.Passengers || flight.DepartureAt <= DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "Sorry, this flight no longer has enough seats.");
        }

        vm.Flight = flight;
        if (!ModelState.IsValid)
        {
            ViewBag.From = from; ViewBag.To = to; ViewBag.Date = date;
            return View(vm);
        }

        var userId = User.GetUserId();
        var now = DateTime.UtcNow;

        // Duplicate detection: same user + same flight already has an active booking
        var duplicate = await DuplicateDetector.FindDuplicateAsync(db: _db, userId: userId, flightId: flight.Id);

        var unitPrice = vm.CabinClass switch
        {
            CabinClass.Business => flight.BusinessPrice,
            CabinClass.First => flight.FirstPrice,
            _ => flight.EconomyPrice
        };

        var booking = new Booking
        {
            Pnr = await NewPnrAsync(),
            UserId = userId,
            FlightId = flight.Id,
            CabinClass = vm.CabinClass,
            PassengerCount = vm.Passengers,
            TotalPrice = unitPrice * vm.Passengers,
            Status = BookingStatus.Pending,
            ContactEmail = vm.ContactEmail.Trim(),
            ContactPhone = vm.ContactPhone?.Trim(),
            PaymentRef = $"PAY-{Random.Shared.Next(10000000, 99999999)}",
            CardLast4 = vm.CardNumber.Length >= 4 ? vm.CardNumber[^4..] : null,
            DuplicateOfId = duplicate?.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Reserve seats (unique index also guards against double-selling)
        var seats = await SeatHelper.AssignSeatsAsync(
            _db, flight.Id, vm.CabinClass, SeatsTotal(flight, vm.CabinClass), vm.Passengers, vm.SeatPreference);

        if (seats.Count < vm.Passengers)
        {
            ModelState.AddModelError(string.Empty, "Not enough seats are free in this class right now.");
            ViewBag.From = from; ViewBag.To = to; ViewBag.Date = date;
            return View(vm);
        }

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(); // booking id needed for passengers

        for (var i = 0; i < vm.PassengerList.Count; i++)
        {
            var p = vm.PassengerList[i];
            _db.Passengers.Add(new Passenger
            {
                BookingId = booking.Id,
                FlightId = flight.Id,
                FullName = p.FullName.Trim(),
                IdType = p.IdType,
                IdNumber = p.IdNumber.Trim(),
                Age = p.Age,
                Gender = p.Gender,
                SeatNumber = seats[i]
            });
        }

        switch (vm.CabinClass)
        {
            case CabinClass.Business: flight.BusinessAvailable -= vm.Passengers; break;
            case CabinClass.First: flight.FirstAvailable -= vm.Passengers; break;
            default: flight.EconomyAvailable -= vm.Passengers; break;
        }
        flight.UpdatedAt = now;

        await _db.SaveChangesAsync();

        await _logger.LogAsync(userId, "Booking Created", "Booking", booking.Id,
            $"Booking #{booking.Id} (PNR {booking.Pnr}) created for flight {flight.FlightNumber}");
        if (duplicate is not null)
            await _logger.LogAsync(userId, "Duplicate Flagged", "Booking", booking.Id,
                $"Auto-duplicate detection matched this booking with #{duplicate.Id} (same user and flight)");

        TempData["Success"] = duplicate is null
            ? $"Booking requested! PNR {booking.Pnr} — our team will verify your payment shortly."
            : $"Booking created with PNR {booking.Pnr}. Note: this looks like a duplicate of booking #{duplicate.Id}; the admin will review it.";
        return RedirectToAction(nameof(Ticket), new { id = booking.Id });
    }

    // ---------------- My bookings ----------------
    public async Task<IActionResult> MyBookings(string? status)
    {
        var userId = User.GetUserId();
        var query = _db.Bookings
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Where(b => b.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, out var parsed))
            query = query.Where(b => b.Status == parsed);

        ViewBag.StatusFilter = status;
        var list = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(list);
    }

    // ---------------- E-ticket ----------------
    public async Task<IActionResult> Ticket(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null) return NotFound();
        if (booking.UserId != User.GetUserId() && !User.IsInRole(nameof(UserRole.Admin))) return Forbid();

        return View(booking);
    }

    // ---------------- Update passengers ----------------
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var booking = await OwnEditableBookingAsync(id);
        if (booking is null) return RedirectToAction(nameof(MyBookings));

        var vm = new BookViewModel
        {
            FlightId = booking.FlightId,
            Flight = booking.Flight,
            CabinClass = booking.CabinClass,
            Passengers = booking.PassengerCount,
            ContactEmail = booking.ContactEmail,
            ContactPhone = booking.ContactPhone,
            PassengerList = booking.Passengers.OrderBy(p => p.SeatNumber).Select(p => new PassengerInput
            {
                FullName = p.FullName,
                IdType = p.IdType,
                IdNumber = p.IdNumber,
                Age = p.Age,
                Gender = p.Gender
            }).ToList()
        };
        ViewBag.BookingId = id;
        ViewBag.ReadonlyPayment = true;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BookViewModel vm)
    {
        var booking = await _db.Bookings
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == User.GetUserId());

        if (booking is null) return RedirectToAction(nameof(MyBookings));
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed) ||
            booking.Flight!.DepartureAt <= DateTime.UtcNow)
        {
            TempData["Error"] = "This booking can no longer be updated.";
            return RedirectToAction(nameof(MyBookings));
        }

        var passengers = booking.Passengers.OrderBy(p => p.SeatNumber).ToList();
        if (vm.PassengerList.Count != passengers.Count)
        {
            TempData["Error"] = "Passenger count cannot change here — cancel and rebook instead.";
            return RedirectToAction(nameof(MyBookings));
        }

        // Skip payment validation on edit
        ModelState.Remove(nameof(vm.CardName));
        ModelState.Remove(nameof(vm.CardNumber));
        ModelState.Remove(nameof(vm.Expiry));
        ModelState.Remove(nameof(vm.Cvv));
        ViewBag.BookingId = id;
        ViewBag.ReadonlyPayment = true;
        vm.Flight = booking.Flight;

        if (!ModelState.IsValid) return View(vm);

        for (var i = 0; i < passengers.Count; i++)
        {
            var p = vm.PassengerList[i];
            passengers[i].FullName = p.FullName.Trim();
            passengers[i].IdType = p.IdType;
            passengers[i].IdNumber = p.IdNumber.Trim();
            passengers[i].Age = p.Age;
            passengers[i].Gender = p.Gender;
        }

        booking.ContactEmail = vm.ContactEmail.Trim();
        booking.ContactPhone = vm.ContactPhone?.Trim();
        booking.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _logger.LogAsync(booking.UserId, "Booking Updated", "Booking", booking.Id,
            $"Passenger details updated for booking #{booking.Id} (PNR {booking.Pnr})");

        TempData["Success"] = "Passenger details updated.";
        return RedirectToAction(nameof(Ticket), new { id });
    }

    // ---------------- Cancel ----------------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == User.GetUserId());

        if (booking is null) return RedirectToAction(nameof(MyBookings));

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            TempData["Error"] = "Only pending or confirmed bookings can be cancelled.";
            return RedirectToAction(nameof(MyBookings));
        }

        if (booking.Flight!.DepartureAt <= DateTime.UtcNow)
        {
            TempData["Error"] = "The flight has already departed — cancellation is closed.";
            return RedirectToAction(nameof(MyBookings));
        }

        ReleaseBooking(booking);
        await _db.SaveChangesAsync();
        await _logger.LogAsync(booking.UserId, "Booking Cancelled", "Booking", booking.Id,
            $"Booking #{booking.Id} (PNR {booking.Pnr}) cancelled by client");

        TempData["Success"] = $"Booking {booking.Pnr} cancelled. Seats are released back to inventory.";
        return RedirectToAction(nameof(MyBookings));
    }

    // ---------------- Helpers ----------------
    private static int SeatsTotal(Flight f, CabinClass c) => c switch
    {
        CabinClass.Business => f.BusinessTotal,
        CabinClass.First => f.FirstTotal,
        _ => f.EconomyTotal
    };

    private static int SeatsLeft(Flight f, CabinClass c) => c switch
    {
        CabinClass.Business => f.BusinessAvailable,
        CabinClass.First => f.FirstAvailable,
        _ => f.EconomyAvailable
    };

    /// <summary>Mark cancelled and give seats back to the flight inventory.</summary>
    private static void ReleaseSeats(Booking booking)
    {
        var f = booking.Flight!;
        switch (booking.CabinClass)
        {
            case CabinClass.Business: f.BusinessAvailable = Math.Min(f.BusinessTotal, f.BusinessAvailable + booking.PassengerCount); break;
            case CabinClass.First: f.FirstAvailable = Math.Min(f.FirstTotal, f.FirstAvailable + booking.PassengerCount); break;
            default: f.EconomyAvailable = Math.Min(f.EconomyTotal, f.EconomyAvailable + booking.PassengerCount); break;
        }
        f.UpdatedAt = DateTime.UtcNow;
    }

    public static void ReleaseBooking(Booking booking)
    {
        ReleaseSeats(booking);
        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        foreach (var p in booking.Passengers) p.SeatNumber = null; // unique index frees the seats
    }

    private async Task<Booking?> OwnEditableBookingAsync(int id) =>
        await _db.Bookings
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == User.GetUserId());

    private async Task<string> NewPnrAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            var pnr = SeatHelper.NewPnr();
            if (!await _db.Bookings.AnyAsync(b => b.Pnr == pnr)) return pnr;
        }
        return SeatHelper.NewPnr() + Random.Shared.Next(10, 99);
    }
}
