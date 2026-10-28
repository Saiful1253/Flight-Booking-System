using FlightBookingSystem.Data;
using FlightBookingSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(GetConnectionString(builder.Configuration)));

builder.Services.AddScoped<ActivityLogger>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

var app = builder.Build();

// Create the database and seed demo data on first run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Render.com (and most PaaS) provide a single DATABASE_URL in postgres://user:pass@host:port/db
// format. Convert it to the Host=...;Username=... format Npgsql expects.
static string GetConnectionString(IConfiguration config)
{
    var cs = config.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(cs)) return cs;

    var url = config["DATABASE_URL"];
    if (string.IsNullOrWhiteSpace(url))
        throw new InvalidOperationException(
            "No database configured. Set ConnectionStrings__DefaultConnection or DATABASE_URL.");

    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);
    var user = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')}" +
           $";Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}
