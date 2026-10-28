using FlightBookingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightBookingSystem.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Airline> Airlines => Set<Airline>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e => e.HasIndex(u => u.Email).IsUnique());
        modelBuilder.Entity<Airport>(e => e.HasIndex(a => a.Code).IsUnique());
        modelBuilder.Entity<Airline>(e => e.HasIndex(a => a.Code).IsUnique());
        modelBuilder.Entity<Booking>(e => e.HasIndex(b => b.Pnr).IsUnique());

        modelBuilder.Entity<Flight>(e =>
        {
            e.HasOne(f => f.Airline).WithMany().HasForeignKey(f => f.AirlineId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.DepartureAirport).WithMany().HasForeignKey(f => f.DepartureAirportId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.ArrivalAirport).WithMany().HasForeignKey(f => f.ArrivalAirportId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(f => new { f.DepartureAirportId, f.ArrivalAirportId, f.DepartureAt });
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.HasOne(b => b.User).WithMany(u => u.Bookings)
             .HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Flight).WithMany()
             .HasForeignKey(b => b.FlightId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.DuplicateOf).WithMany()
             .HasForeignKey(b => b.DuplicateOfId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(b => new { b.UserId, b.FlightId, b.Status });
        });

        modelBuilder.Entity<Passenger>(e =>
        {
            e.HasOne(p => p.Booking).WithMany(b => b.Passengers)
             .HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
            // One seat can never be sold twice on the same flight (NULL = seat released)
            e.HasIndex(p => new { p.FlightId, p.SeatNumber }).IsUnique();
        });

        modelBuilder.Entity<ActivityLog>(e =>
        {
            e.HasOne(l => l.User).WithMany()
             .HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
