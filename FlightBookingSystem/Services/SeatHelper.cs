using FlightBookingSystem.Data;
using FlightBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightBookingSystem.Services;

/// <summary>PNR codes + seat layout helpers.</summary>
public static class SeatHelper
{
    private static readonly string[] EconomyLetters = { "A", "B", "C", "D", "E", "F" };
    private static readonly string[] BusinessLetters = { "A", "B", "C", "D" };
    private static readonly string[] FirstLetters = { "A", "B", "C" };

    /// <summary>Full seat list for a cabin class, e.g. 1A, 1B ... 25F.</summary>
    public static List<string> GenerateSeats(int count, CabinClass cabin)
    {
        var letters = cabin switch
        {
            CabinClass.Business => BusinessLetters,
            CabinClass.First => FirstLetters,
            _ => EconomyLetters
        };

        var seats = new List<string>(count);
        var row = 1;
        var i = 0;
        while (seats.Count < count)
        {
            foreach (var l in letters)
            {
                if (seats.Count >= count) break;
                seats.Add($"{row}{l}");
            }
            row++;
            i++;
        }
        return seats;
    }

    /// <summary>Seats already sold on this flight (from active bookings only).</summary>
    public static async Task<HashSet<string>> TakenSeatsAsync(AppDbContext db, int flightId)
    {
        var seats = await db.Passengers
            .Where(p => p.FlightId == flightId && p.SeatNumber != null)
            .Select(p => p.SeatNumber!)
            .Distinct()
            .ToListAsync();
        return seats.ToHashSet();
    }

    /// <summary>Picks seats for new passengers, preferring the requested letter when possible.</summary>
    public static async Task<List<string>> AssignSeatsAsync(
        AppDbContext db, int flightId, CabinClass cabin, int seatCount, int passengerCount, string? preferredLetter)
    {
        var all = GenerateSeats(seatCount, cabin);
        var taken = await TakenSeatsAsync(db, flightId);
        var free = all.Where(s => !taken.Contains(s)).ToList();

        var chosen = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredLetter))
        {
            var pref = free.Where(s => s.EndsWith(preferredLetter.ToUpperInvariant(), StringComparison.Ordinal)).ToList();
            foreach (var s in pref.Take(passengerCount)) { chosen.Add(s); free.Remove(s); }
        }
        foreach (var s in free.Take(passengerCount - chosen.Count)) chosen.Add(s);

        return chosen;
    }

    public static string NewPnr()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = RandomNumberGenerator();
        var arr = new char[6];
        for (var i = 0; i < arr.Length; i++) arr[i] = chars[rng.Next(chars.Length)];
        return new string(arr);

        static Random RandomNumberGenerator() => Random.Shared;
    }
}
