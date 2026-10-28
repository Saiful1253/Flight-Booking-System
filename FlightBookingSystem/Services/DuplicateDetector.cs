using FlightBookingSystem.Data;
using FlightBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightBookingSystem.Services;

/// <summary>Flags bookings where the same user books the same flight twice.</summary>
public static class DuplicateDetector
{
    public static async Task<Booking?> FindDuplicateAsync(AppDbContext db, int userId, int flightId)
    {
        return await db.Bookings.FirstOrDefaultAsync(b =>
            b.UserId == userId &&
            b.FlightId == flightId &&
            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
    }

    public static async Task<List<Booking>> GetFlaggedAsync(AppDbContext db) =>
        await db.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight).ThenInclude(f => f.Airline)
            .Include(b => b.Flight).ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight).ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.DuplicateOf)
            .Where(b => b.DuplicateOfId != null &&
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
}
