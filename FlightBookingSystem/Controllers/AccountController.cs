using System.Security.Claims;
using FlightBookingSystem.Data;
using FlightBookingSystem.Models;
using FlightBookingSystem.Services;
using FlightBookingSystem.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FlightBookingSystem.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();
    private readonly ActivityLogger _logger;

    public AccountController(AppDbContext db, ActivityLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated ?? false) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["AuthRole"] = "client";
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, string? portal = null, string? returnUrl = null)
    {
        var wantAdmin = portal == "admin";
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["AuthRole"] = wantAdmin ? "admin" : "client";
        if (!ModelState.IsValid) return View(vm);

        var user = _db.Users.FirstOrDefault(u => u.Email == vm.Email.Trim().ToLower());
        if (user is null || !user.IsActive ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(vm);
        }

        // Portal must match the account role (Traveller ↔ Client, Staff ↔ Admin)
        if (wantAdmin && user.Role != UserRole.Admin)
        {
            ModelState.AddModelError(string.Empty, "The Staff portal only accepts staff accounts. Switch to the Traveller portal to sign in.");
            return View(vm);
        }
        if (!wantAdmin && user.Role == UserRole.Admin)
        {
            ModelState.AddModelError(string.Empty, "This is a staff account. Switch to the Staff portal to sign in.");
            return View(vm);
        }

        await SignInAsync(user);
        await _logger.LogAsync(user.Id, "Login", "User", user.Id, $"{user.FullName} signed in");

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return user.Role == UserRole.Admin
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated ?? false) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        vm.Role = UserRole.Client; // admins are seeded by the system
        if (!ModelState.IsValid) return View(vm);

        var email = vm.Email.Trim().ToLower();
        if (_db.Users.Any(u => u.Email == email))
        {
            ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists.");
            return View(vm);
        }

        var user = new User
        {
            FullName = vm.FullName.Trim(),
            Email = email,
            Phone = vm.Phone,
            Role = UserRole.Client,
            PasswordHash = _hasher.HashPassword(new User(), vm.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _logger.LogAsync(user.Id, "Registration", "User", user.Id, $"{user.FullName} created an account");

        await SignInAsync(user);
        TempData["Success"] = "Welcome to FlyLocal! Your account is ready.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var id = User.Identity;
        if (id?.IsAuthenticated ?? false)
            await _logger.LogAsync(null, "Logout", "User", null, $"{id.Name} signed out");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    private async Task SignInAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
