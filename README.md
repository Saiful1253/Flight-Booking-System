# FlyLocal — Flight Booking System

Professional flight booking system built with **C# (ASP.NET Core 8 MVC)** and **PostgreSQL**.
Three-part design: public booking site + **Client panel** + **Admin panel**.

![Stack](https://img.shields.io/badge/.NET-8.0-512BD4) ![DB](https://img.shields.io/badge/PostgreSQL-17-336791)

## 🌐 Live Demo

**https://flight-booking-system-cmvt.onrender.com**

> First visit may take ~50 seconds (free tier instance wakes up on demand).

| Role | Email | Password |
|---|---|---|
| Admin | `admin@flylocal.local` | `Admin@123` |
| Client | `client@flylocal.local` | `Client@123` |

---

## Features

### Public site
- Flight search (from / to / date / passengers / cabin class)
- Popular destination cards (Dhaka, Chattogram, Sylhet, Cox's Bazar, Dubai, Bangkok, Kuala Lumpur)
- Responsive professional design (Bootstrap 5 + custom aviation theme, free Wikimedia photos)

### Client panel
| Requirement | Implementation |
|---|---|
| Account registration & login | Cookie auth with roles (`AccountController`) |
| Book a flight | Search → select → passenger details → demo payment → PNR |
| Select class, location, date, passengers | Search form + per-class pricing (Economy / Business / First) |
| Track booking status | My bookings + e-ticket page with live status |
| Update booking | Edit passenger details & contact before departure |
| Cancel booking | Cancel releases seats back to inventory |
| Previous booking history | My bookings list with status filters |

### Admin panel
| Requirement | Implementation |
|---|---|
| See all bookings | Bookings list with status/class/search filters |
| Verify or reject bookings | Approve (confirm) / Reject with reason (seats auto-released) |
| Priority handling | Approval queue sorted by **departure soonest first** |
| Manage "resources" | **Flight inventory** (flights, seats, prices, status) + airlines + airports |
| Duplicate detection | Auto-flags booking when the same user books the same flight twice → Duplicates page (clear or merge) |
| Reports & activity logs | Reports page (revenue, occupancy, routes, airlines) + full audit trail |

---

## Tech stack

- ASP.NET Core 8 MVC (Razor views), Entity Framework Core 8 + **Npgsql (PostgreSQL)**
- ASP.NET Core Cookie Authentication with roles (Client / Admin)
- Password hashing via `PasswordHasher<TUser>`
- Bootstrap 5.3 + Bootstrap Icons + Chart.js (admin charts)
- Images: free-license photos from Wikimedia Commons (downloaded into `wwwroot/images`)

## Requirements

- .NET SDK 8
- PostgreSQL 14+ (this project was built & tested on PostgreSQL 17)

## Setup

1. **Create the database** (once):

   ```powershell
   & "C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -c "CREATE DATABASE flight_booking;"
   ```

2. **Create your local settings file** from the example:

   ```powershell
   Copy-Item .\FlightBookingSystem\appsettings.example.json .\FlightBookingSystem\appsettings.json
   ```

   Set your PostgreSQL password in `FlightBookingSystem\appsettings.json`. This local
   file is ignored by Git so database credentials are not published.

3. **Run**:

   ```powershell
   cd FlightBookingSystem
   dotnet run
   ```

   On first run the app **creates the database tables automatically** and seeds demo
   users, airlines, airports, flights, bookings and activity logs.

4. Open `http://localhost:5088` (or the URL shown in the console).

## Demo accounts

| Role | Email | Password |
|---|---|---|
| Admin | `admin@flylocal.local` | `Admin@123` |
| Client | `client@flylocal.local` | `Client@123` |
| Client | `sumaiya@flylocal.local` | `Client@123` |

## Database schema (PostgreSQL)

- `Users` — clients & admins (email unique, hashed password, role)
- `Airports` — IATA code, name, city, country
- `Airlines` — name, code, brand colour
- `Flights` — number, airline, route, times, status, seats & prices per cabin class
- `Bookings` — PNR (unique), user, flight, class, price, status, payment info, duplicate flag
- `Passengers` — per booking, with assigned seat; **unique index on (FlightId, SeatNumber)** so a seat can never be sold twice
- `ActivityLogs` — audit trail of every action

## Notes

- All times are stored in **UTC**.
- Payment is a **demo checkout** (no real gateway) — bookings start as *Pending* and the admin confirms them.
- Duplicate detection: same user + same flight + active booking → auto-flagged.
- Photos are from Wikimedia Commons (free licences); replace files in `wwwroot/images` with your own if you prefer.
