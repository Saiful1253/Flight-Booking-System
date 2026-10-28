using FlightBookingSystem.Data;
using FlightBookingSystem.Models;
using FlightBookingSystem.Services;
using FlightBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlightBookingSystem.Controllers;

[Authorize(Roles = nameof(UserRole.Client))]
public class ClientController : Controller
{
    private readonly AppDbContext _db;

    public ClientController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId();
        var bookings = await _db.Bookings
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var vm = new ClientDashboardViewModel
        {
            TotalBookings = bookings.Count,
            UpcomingTrips = bookings.Count(b => b.Status == BookingStatus.Confirmed && b.Flight!.DepartureAt > now),
            CancelledCount = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            Upcoming = bookings
                .Where(b => b.IsActive && b.Flight!.DepartureAt > now)
                .OrderBy(b => b.Flight!.DepartureAt)
                .ToList(),
            Recent = bookings.Take(5).ToList()
        };
        return View(vm);
    }
}
