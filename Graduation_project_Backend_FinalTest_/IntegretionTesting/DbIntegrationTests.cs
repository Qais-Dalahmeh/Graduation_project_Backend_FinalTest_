using FluentAssertions;
using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests;

[Collection("db")]
public class DbIntegrationTests
{
    private readonly DatabaseFixture _fx;

    public DbIntegrationTests(DatabaseFixture fx) => _fx = fx;

    private AppDbContext GetDb(out IServiceScope scope)
    {
        scope = _fx.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    // ---------------------------
    // UserProfiles / UserSessions
    // ---------------------------

    [Fact]
    public async Task UserProfile_Create_Works_And_Defaults_Applied()
    {
        await _fx.ResetAsync();

        using var dbScope = _fx.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            PhoneNumber = "0790000000",
            PasswordHash = "hash"
            // TotalPoints + Role عليهم defaults
        };

        db.UserProfiles.Add(user);
        await db.SaveChangesAsync();

        var found = await db.UserProfiles.SingleAsync(x => x.PhoneNumber == "0790000000");
        found.TotalPoints.Should().Be(0);
        found.Role.Should().Be("user");
    }

    [Fact]
    public async Task UserProfile_PhoneNumber_Unique_Enforced()
    {
        await _fx.ResetAsync();

        using var dbScope = _fx.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = "A",
            PhoneNumber = "0791111111",
            PasswordHash = "hash"
        });

        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = "B",
            PhoneNumber = "0791111111", // duplicate
            PasswordHash = "hash"
        });

        await FluentActions.Invoking(() => db.SaveChangesAsync())
            .Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task UserSession_FK_And_Cascade_Delete_Works()
    {
        await _fx.ResetAsync();

        using var dbScope = _fx.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile
        {
            Id = userId,
            Name = "Sess User",
            PhoneNumber = "0792222222",
            PasswordHash = "hash"
        });
        await db.SaveChangesAsync();

        db.UserSessions.Add(new UserSession
        {
            Id = "sess_1",
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow

        });
        await db.SaveChangesAsync();

        // delete user -> cascade should delete sessions
        var u = await db.UserProfiles.FindAsync(userId);
        db.UserProfiles.Remove(u!);
        await db.SaveChangesAsync();

        (await db.UserSessions.AnyAsync(s => s.Id == "sess_1")).Should().BeFalse();
    }

    // ---------------------------
    // Stores
    // ---------------------------

    [Fact]
    public async Task Store_Create_Works()
    {
        await _fx.ResetAsync();

        using var dbScope = _fx.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var store = new Store { Id = Guid.NewGuid(), Name = "Store 1" };
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        (await db.Stores.AnyAsync(x => x.Name == "Store 1")).Should().BeTrue();
    }

    // ---------------------------
    // Transactions
    // ---------------------------

    [Fact]
    public async Task Transaction_ReceiptId_Unique_Enforced()
    {
        await _fx.ResetAsync();

        using var dbScope = _fx.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // seed user + store لأن Transaction requires FK fields
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.UserProfiles.Add(new UserProfile
        {
            Id = userId,
            Name = "Buyer",
            PhoneNumber = "0793333333",
            PasswordHash = "hash"
        });

        db.Stores.Add(new Store
        {
            Id = storeId,
            Name = "Store A"
        });

        await db.SaveChangesAsync();

        db.Transactions.Add(new Transaction
        {
            // Id generated
            UserId = userId,
            StoreId = storeId,
            ReceiptId = "R-123",
            Price = 10.50m,
            Points = 5,
            ReceiptDescription = "desc"
            // CreatedAt default now()
        });

        db.Transactions.Add(new Transaction
        {
            UserId = userId,
            StoreId = storeId,
            ReceiptId = "R-123", // duplicate receipt
            Price = 7.00m,
            Points = 3
        });

        await FluentActions.Invoking(() => db.SaveChangesAsync())
            .Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Transaction_User_Delete_Restricted()
    {
        await _fx.ResetAsync();

        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        
        using (var scope = _fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.UserProfiles.Add(new UserProfile
            {
                Id = userId,
                Name = "Buyer",
                PhoneNumber = "0794444444",
                PasswordHash = "hash"
            });

            db.Stores.Add(new Store
            {
                Id = storeId,
                Name = "Store B"
            });

            await db.SaveChangesAsync();

            db.Transactions.Add(new Transaction
            {
                UserId = userId,
                StoreId = storeId,
                ReceiptId = $"R-{Guid.NewGuid():N}",
                Price = 20m,
                Points = 10,
                ReceiptDescription = "test receipt" 
            });


            await db.SaveChangesAsync();
        }

        // 2) Try delete user in a NEW context (no tracked dependents)
        using (var scope = _fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var u = await db.UserProfiles.FindAsync(userId);
            db.UserProfiles.Remove(u!);

            await FluentActions.Invoking(() => db.SaveChangesAsync())
                .Should().ThrowAsync<DbUpdateException>();
        }
    }


    // ---------------------------
    // Coupons / UserCoupons
    // ---------------------------

    [Fact]
    public async Task UserCoupon_Cascade_Delete_Works_User_And_Coupon()
    {
        await _fx.ResetAsync();

        using var dbScope = _fx.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = Guid.NewGuid();
        db.UserProfiles.Add(new UserProfile
        {
            Id = userId,
            Name = "U",
            PhoneNumber = "0795555555",
            PasswordHash = "hash"
        });

        var coupon = new Coupon
        {
            // Id generated
            ManagerId = Guid.NewGuid(),
            Type = "discount",
            StartAt = DateTimeOffset.UtcNow.AddDays(-1),
            EndAt = DateTimeOffset.UtcNow.AddDays(10),
            CostPoint = 100
        };

        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();

        db.UserCoupons.Add(new UserCoupon
        {
            SerialNumber = "A1B2C3D4", // 8 chars
            UserId = userId,
            CouponId = coupon.Id,
            IsRedeemed = false
            // CreatedAt default now()
        });

        await db.SaveChangesAsync();

        // delete coupon -> cascades user_coupon row
        db.Coupons.Remove(coupon);
        await db.SaveChangesAsync();

        (await db.UserCoupons.AnyAsync(x => x.SerialNumber == "A1B2C3D4")).Should().BeFalse();
    }
}
