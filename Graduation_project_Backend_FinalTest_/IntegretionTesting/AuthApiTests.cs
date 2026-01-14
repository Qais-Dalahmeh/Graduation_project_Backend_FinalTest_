using FluentAssertions;
using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.DTOs;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests;

[Collection("db")]
public class AuthApiTests
{
    private readonly DatabaseFixture _fx;
    private static readonly PasswordHasher<UserProfile> Hasher = new();

    public AuthApiTests(DatabaseFixture fx) => _fx = fx;

    private async Task<UserProfile?> FindUserAsync(string rawPhone, Guid mallId)
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ServiceClass>();

        var normalizedPhone = service.NormalizePhone(rawPhone);
        return await db.UserProfiles.SingleOrDefaultAsync(u => u.PhoneNumber == normalizedPhone && u.MallID == mallId);
    }


    private async Task SeedUserAsync(string rawPhone, string password, Guid mallId, string name = "Seed")
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ServiceClass>();

        var normalizedPhone = service.NormalizePhone(rawPhone);

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            PhoneNumber = normalizedPhone,
            Name = name,
            Role = "user",
            TotalPoints = 0,
            MallID = mallId
        };

        user.PasswordHash = Hasher.HashPassword(user, password);

        db.UserProfiles.Add(user);
        await db.SaveChangesAsync();
    }


    [Fact]
    public async Task LoginOrRegister_Register_NewUser_ReturnsRegistered_AndPersistsUser()
    {
        await _fx.ResetAsync();

        var mallId = Guid.NewGuid();
        var dto = new LoginOrRegisterDto
        {
            PhoneNumber = "0790000000",
            Password = "P@ssw0rd!",
            Name = "Nevien",
            MallID = mallId
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Auth/login-or-register", dto);

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("Registered");
        body.PhoneNumber.Should().NotBeNullOrWhiteSpace();
        body.UserId.Should().NotBe(Guid.Empty);

       
        var saved = await FindUserAsync(body.PhoneNumber!, mallId);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Nevien");
    }

    [Fact]
    public async Task LoginOrRegister_Login_ExistingUser_CorrectPassword_ReturnsLoggedIn()
    {
        await _fx.ResetAsync();

        var mallId = Guid.NewGuid();

        await SeedUserAsync("0791111111", "Secret123!", mallId, "Existing");

        var dto = new LoginOrRegisterDto
        {
            PhoneNumber = "0791111111",
            Password = "Secret123!",
            Name = "Ignored",
            MallID = mallId
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Auth/login-or-register", dto);

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadFromJsonAsync<AuthResponseDto>();
        body.Should().NotBeNull();
        body!.Message.Should().Be("LoggedIn");
        body.Name.Should().Be("Existing");
    }

    [Fact]
    public async Task LoginOrRegister_Login_ExistingUser_WrongPassword_Returns401()
    {
        await _fx.ResetAsync();

        var mallId = Guid.NewGuid();
        await SeedUserAsync("0792222222", "RightPass!", mallId, "Existing");

        var dto = new LoginOrRegisterDto
        {
            PhoneNumber = "0792222222",
            Password = "WrongPass!",
            MallID = mallId
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Auth/login-or-register", dto);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var text = await res.Content.ReadAsStringAsync();
        text.Should().Contain("Invalid phone number or password");
    }

    [Fact]
    public async Task LoginOrRegister_MissingPhoneOrPassword_Returns400()
    {
        await _fx.ResetAsync();

        var dto = new LoginOrRegisterDto
        {
            PhoneNumber = "",
            Password = "",
            MallID = Guid.NewGuid()
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Auth/login-or-register", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var text = await res.Content.ReadAsStringAsync();
        text.Should().Contain("PhoneNumber and Password are required");
    }

    [Fact]
    public async Task LoginOrRegister_InvalidPhone_Returns400_WithErrorCode()
    {
        await _fx.ResetAsync();

        var dto = new LoginOrRegisterDto
        {
            PhoneNumber = "not-a-phone",
            Password = "AnyPass123!",
            MallID = Guid.NewGuid()
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Auth/login-or-register", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var json = await res.Content.ReadAsStringAsync();
        json.Should().Contain("INVALID_PHONE_NUMBER");
        json.Should().Contain("phoneNumber");
    }
}
