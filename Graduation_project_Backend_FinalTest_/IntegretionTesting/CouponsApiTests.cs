using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.DTOs;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;

namespace Graduation_Project_Backend.IntegrationTests;

[Collection("db")]
public class CouponsApiTests
{
    private readonly DatabaseFixture _fx;
    private static readonly PasswordHasher<UserProfile> Hasher = new();

    public CouponsApiTests(DatabaseFixture fx) => _fx = fx;

    private async Task<Guid> SeedUserAsync(string phone, Guid mallId)
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            Name = "User",
            Role = "user",
            TotalPoints = 500,
            MallID = mallId
        };
        user.PasswordHash = Hasher.HashPassword(user, "pass");

        db.UserProfiles.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Coupon> SeedCouponAsync(Guid managerId, bool isActive = true, int costPoint = 100)
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var coupon = new Coupon
        {
            ManagerId = managerId,
            Type = "discount",
            StartAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt = DateTimeOffset.UtcNow.AddDays(7),
            Discription = "test",
            IsActive = isActive,
            CostPoint = costPoint
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return coupon;
    }

    [Fact]
    public async Task GetCoupons_NoFilter_Returns200()
    {
        await _fx.ResetAsync();

        // seed a couple coupons
        await SeedCouponAsync(Guid.NewGuid(), isActive: true);
        await SeedCouponAsync(Guid.NewGuid(), isActive: false);

        var res = await _fx.Client.GetAsync("/api/Coupons");

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        
        var json = await res.Content.ReadAsStringAsync();
        json.TrimStart().Should().StartWith("[");
    }

    [Fact]
    public async Task GetCoupons_FilterActiveTrue_Returns200()
    {
        await _fx.ResetAsync();

        await SeedCouponAsync(Guid.NewGuid(), isActive: true);
        await SeedCouponAsync(Guid.NewGuid(), isActive: false);

        var res = await _fx.Client.GetAsync("/api/Coupons?isActive=true");

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadAsStringAsync();
        json.TrimStart().Should().StartWith("[");
    }

    [Fact]
    public async Task GetCouponById_NotFound_Returns404()
    {
        await _fx.ResetAsync();

        var res = await _fx.Client.GetAsync($"/api/Coupons/{Guid.NewGuid()}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var text = await res.Content.ReadAsStringAsync();
        text.Should().Contain("Coupon not found");
    }

    [Fact]
    public async Task GetCouponById_Existing_Returns200()
    {
        await _fx.ResetAsync();

        var coupon = await SeedCouponAsync(Guid.NewGuid(), isActive: true);

        var res = await _fx.Client.GetAsync($"/api/Coupons/{coupon.Id}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RedeemCoupon_Success_ReturnsSerialNumber()
    {
        await _fx.ResetAsync();

        var mallId = Guid.NewGuid();
        var userId = await SeedUserAsync("0797777777", mallId);
        var coupon = await SeedCouponAsync(Guid.NewGuid(), isActive: true, costPoint: 50);

        var dto = new RedeemCouponDto
        {
            UserId = userId,
            CouponId = coupon.Id
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Coupons/redeem", dto);

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // expected: { message: "...", serial_number: "XXXXXXXX" }
        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString().Should().Contain("Coupon redeemed successfully");
        doc.RootElement.TryGetProperty("serial_number", out var sn).Should().BeTrue();
        sn.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RedeemCoupon_InvalidUserOrCoupon_Returns400()
    {
        await _fx.ResetAsync();

        var dto = new RedeemCouponDto
        {
            UserId = Guid.NewGuid(),
            CouponId = Guid.NewGuid()
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Coupons/redeem", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RedeemBySerial_InvalidSerial_Returns400()
    {
        await _fx.ResetAsync();

        var dto = new RedeemCouponBySerialDto
        {
            SerialNumber = "ZZZZZZZZ"
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Coupons/redeem-by-serial", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUserCoupons_Returns200()
    {
        await _fx.ResetAsync();

        var mallId = Guid.NewGuid();
        var userId = await SeedUserAsync("0798888888", mallId);
        var coupon = await SeedCouponAsync(Guid.NewGuid(), isActive: true);

        // seed user_coupon so the endpoint returns something
        using (var scope = _fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserCoupons.Add(new UserCoupon
            {
                SerialNumber = "A1B2C3D4",
                UserId = userId,
                CouponId = coupon.Id,
                IsRedeemed = false
            });
            await db.SaveChangesAsync();
        }

        var res = await _fx.Client.GetAsync($"/api/Coupons/user/{userId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadAsStringAsync();
        json.TrimStart().Should().StartWith("[");
    }
}
