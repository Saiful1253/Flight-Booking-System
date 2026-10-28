using FlightBookingSystem.Data;
using FlightBookingSystem.Models;
using FlightBookingSystem.Services;
using FlightBookingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FlightBookingSystem.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    // ---------------- Landing page + flight search ----------------
    public async Task<IActionResult> Index()
    {
        var vm = new FlightSearchViewModel
        {
            Airports = await _db.Airports.OrderBy(a => a.City).ToListAsync()
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Search(FlightSearchViewModel vm)
    {
        vm.Airports = await _db.Airports.OrderBy(a => a.City).ToListAsync();

        if (vm.FromAirportId == vm.ToAirportId)
        {
            ModelState.AddModelError(nameof(vm.ToAirportId), "Origin and destination must be different.");
            return View(nameof(Index), vm);
        }

        var fromItems = new SelectList(vm.Airports.Select(a => new { a.Id, Label = $"{a.Code} — {a.City}" }), "Id", "Label", vm.FromAirportId);
        var toItems = new SelectList(vm.Airports.Select(a => new { a.Id, Label = $"{a.Code} — {a.City}" }), "Id", "Label", vm.ToAirportId);

        ViewBag.From = fromItems;
        ViewBag.To = toItems;

        // Treat the picked date as a UTC day (PostgreSQL timestamptz requires UTC kinds)
        var dayStart = DateTime.SpecifyKind(vm.Date.Date, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var flights = await _db.Flights
            .Include(f => f.Airline)
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => f.DepartureAirportId == vm.FromAirportId
                        && f.ArrivalAirportId == vm.ToAirportId
                        && f.DepartureAt >= dayStart
                        && f.DepartureAt < dayEnd
                        && f.Status != FlightStatus.Cancelled)
            .OrderBy(f => f.DepartureAt)
            .ToListAsync();

        var results = flights.Select(f =>
        {
            var (price, left) = vm.CabinClass switch
            {
                CabinClass.Business => (f.BusinessPrice, f.BusinessAvailable),
                CabinClass.First => (f.FirstPrice, f.FirstAvailable),
                _ => (f.EconomyPrice, f.EconomyAvailable)
            };
            return new FlightResultItem
            {
                Flight = f,
                UnitPrice = price,
                SeatsLeft = left,
                Available = f.Status == FlightStatus.Scheduled && left >= vm.Passengers
            };
        }).ToList();

        ViewBag.Search = vm;
        ViewBag.HasSearch = true;
        return View(results);
    }

    public IActionResult Error() => View();
}
