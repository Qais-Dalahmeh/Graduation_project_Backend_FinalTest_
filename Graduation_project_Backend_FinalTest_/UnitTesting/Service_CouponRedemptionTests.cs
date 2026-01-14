using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Graduation_Project_Backend.Tests.Service;

public class CouponRedemptionTests
{
    private readonly ITestOutputHelper _output;

    public CouponRedemptionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task RedeemCouponAsync_ActiveWithinPeriod_DeductsPointsAndCreatesUserCoupon()
    {
        try
        {
            using var db = DbFactory.CreateInMemoryDb();
            var svc = new ServiceClass(db);

            var userId = Guid.NewGuid();
            var couponId = Guid.NewGuid();
            var mallId = Guid.NewGuid();

            _output.WriteLine($"UserId: {userId}");
            _output.WriteLine($"CouponId: {couponId}");
            _output.WriteLine($"MallId: {mallId}");

            var user = new UserProfile
            {
                Id = userId,
                TotalPoints = 500,
                MallID = mallId,
                PhoneNumber = "+962797654321"
            };
            db.UserProfiles.Add(user);

            var now = DateTime.UtcNow;
            var coupon = new Coupon
            {
                Id = couponId,
                IsActive = true,
                StartAt = now.AddDays(-1),
                EndAt = now.AddDays(1),
                CostPoint = 200,
                CreatedAt = now,
                Type = "discount",
                MallID = mallId
            };
            db.Coupons.Add(coupon);

            await db.SaveChangesAsync();

            // show persisted data after initial save
            var savedUser = await db.UserProfiles.AsNoTracking().SingleAsync(u => u.Id == userId);
            _output.WriteLine($"[Before Redeem] User: Id={savedUser.Id}, TotalPoints={savedUser.TotalPoints}, MallID={savedUser.MallID}, Phone={savedUser.PhoneNumber}");
            var savedCoupon = await db.Coupons.AsNoTracking().SingleAsync(c => c.Id == couponId);
            _output.WriteLine($"[Before Redeem] Coupon: Id={savedCoupon.Id}, CostPoint={savedCoupon.CostPoint}, IsActive={savedCoupon.IsActive}, StartAt={savedCoupon.StartAt}, EndAt={savedCoupon.EndAt}");

            var userCoupon = await svc.RedeemCouponAsync(userId, couponId);

            // show created userCoupon returned from service
            _output.WriteLine($"[Redeem Result] UserCoupon: Serial={userCoupon.SerialNumber}, UserId={userCoupon.UserId}, CouponId={userCoupon.CouponId}, IsRedeemed={userCoupon.IsRedeemed}, CreatedAt={userCoupon.CreatedAt}");

            // show persisted UserCoupons in DB
            var allUc = await db.UserCoupons.AsNoTracking().Where(uc => uc.UserId == userId).ToListAsync();
            _output.WriteLine($"[DB] UserCoupons count for user {userId}: {allUc.Count}");
            foreach (var uc in allUc)
            {
                _output.WriteLine($"[DB] UserCoupon row: Serial={uc.SerialNumber}, CouponId={uc.CouponId}, IsRedeemed={uc.IsRedeemed}, CreatedAt={uc.CreatedAt}");
            }

            Assert.Equal(userId, userCoupon.UserId);
            Assert.Equal(couponId, userCoupon.CouponId);
            Assert.False(userCoupon.IsRedeemed);
            Assert.False(string.IsNullOrWhiteSpace(userCoupon.SerialNumber));

            var updatedUser = await svc.GetUserByIdAsync(userId);

            // show updated user after redeem
            _output.WriteLine($"[After Redeem] User: Id={updatedUser!.Id}, TotalPoints={updatedUser.TotalPoints}, Phone={updatedUser.PhoneNumber}");

            Assert.Equal(300, updatedUser!.TotalPoints);

            // Summary and explicit pass confirmation
            _output.WriteLine("--------------------------------------------------");
            _output.WriteLine($"TEST SUMMARY: RedeemCouponAsync_ActiveWithinPeriod\n  UserId: {userId}\n  InitialPoints: 500\n  CostPoint: {savedCoupon.CostPoint}\n  ExpectedRemaining: 300\n  ActualRemaining: {updatedUser.TotalPoints}\n  CreatedUserCouponSerial: {userCoupon.SerialNumber}\n  UserCouponsPersisted: {allUc.Count}");
            _output.WriteLine("[PASS] RedeemCouponAsync_ActiveWithinPeriod - points deducted and user coupon created as expected.");
            _output.WriteLine("--------------------------------------------------");
        }
        catch (Exception ex)
        {
            _output.WriteLine("❌ TEST FAILED");
            _output.WriteLine(ex.GetType().Name);
            _output.WriteLine(ex.Message);
            _output.WriteLine(ex.StackTrace);
            throw; // important so xUnit treats test as failed
        }
    }

    [Fact]
    public async Task RedeemCouponAsync_WhenCouponInactive_Throws()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var userId = Guid.NewGuid();
        var couponId = Guid.NewGuid();

        _output.WriteLine($"Test: RedeemCouponAsync_WhenCouponInactive_Throws - UserId={userId}, CouponId={couponId}");

        db.UserProfiles.Add(new UserProfile { Id = userId, TotalPoints = 1000, MallID = Guid.NewGuid(), PhoneNumber = "+962791234567" });
        db.Coupons.Add(new Coupon
        {
            Id = couponId,
            IsActive = false,
            StartAt = DateTime.UtcNow.AddDays(-1),
            EndAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponAsync(userId, couponId));
        Assert.Equal("Coupon is not active", ex.Message);

        _output.WriteLine("[PASS] RedeemCouponAsync_WhenCouponInactive_Throws - exception thrown as expected: 'Coupon is not active'");
    }

    [Fact]
    public async Task RedeemCouponBySerialAsync_FirstTime_SetsIsRedeemedTrue()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var couponId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var coupon = new Coupon
        {
            Id = couponId,
            IsActive = true,
            StartAt = now.AddDays(-1),
            EndAt = now.AddDays(1),
            CreatedAt = now
        };

        var uc = new UserCoupon
        {
            SerialNumber = "12345678",
            UserId = userId,
            CouponId = couponId,
            Coupon = coupon,
            IsRedeemed = false,
            CreatedAt = now
        };

        _output.WriteLine($"Test: RedeemCouponBySerialAsync_FirstTime_SetsIsRedeemedTrue - Serial={uc.SerialNumber}, UserId={userId}, CouponId={couponId}");

        db.Coupons.Add(coupon);
        db.UserCoupons.Add(uc);
        await db.SaveChangesAsync();

        var redeemed = await svc.RedeemCouponBySerialAsync(" 12345678 ");

        Assert.True(redeemed.IsRedeemed);

        // confirm persisted
        var again = await db.UserCoupons.SingleAsync(x => x.SerialNumber == "12345678");
        Assert.True(again.IsRedeemed);

        _output.WriteLine($"[PASS] RedeemCouponBySerialAsync_FirstTime_SetsIsRedeemedTrue - Serial=12345678 marked redeemed and persisted");
    }

    [Fact]
    public async Task RedeemCouponBySerialAsync_AlreadyRedeemed_Throws()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var couponId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var coupon = new Coupon
        {
            Id = couponId,
            IsActive = true,
            StartAt = now.AddDays(-1),
            EndAt = now.AddDays(1),
            CreatedAt = now
        };

        var serial = "99990000";
        _output.WriteLine($"Test: RedeemCouponBySerialAsync_AlreadyRedeemed_Throws - Serial={serial}, UserId={userId}, CouponId={couponId}");

        db.UserCoupons.Add(new UserCoupon
        {
            SerialNumber = serial,
            UserId = userId,
            CouponId = couponId,
            Coupon = coupon,
            IsRedeemed = true,
            CreatedAt = now
        });
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RedeemCouponBySerialAsync(serial));
        Assert.Equal("Coupon already redeemed", ex.Message);

        _output.WriteLine("[PASS] RedeemCouponBySerialAsync_AlreadyRedeemed_Throws - exception thrown as expected: 'Coupon already redeemed'");
    }
}
