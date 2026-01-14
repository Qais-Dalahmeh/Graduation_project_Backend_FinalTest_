using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Tests.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Graduation_Project_Backend.Tests.Service;

public class TransactionTests
{
    private readonly ITestOutputHelper _output;

    public TransactionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ProcessTransactionAsync_HappyPath_CreatesTransactionAndAddsPoints()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var mallId = Guid.NewGuid();

        var store = new Store { Id = Guid.NewGuid(), Name = "S1", MallID = mallId };
        db.Stores.Add(store);

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            Name = "U1",
            MallID = mallId,
            PhoneNumber = "+962791234567",
            TotalPoints = 0
        };
        db.UserProfiles.Add(user);
        await db.SaveChangesAsync();

        var result = await svc.ProcessTransactionAsync(
            phoneNumber: "+962791234567",
            storeId: store.Id,
            receiptId: "R-100",
            receiptDescription: "desc",
            price: 2.50m
        );

        _output.WriteLine($"Transaction created Id={result.TransactionId}, Receipt={result.ReceiptId}, Points={result.Points}, NewTotal={result.NewTotalPoints}");

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(store.Id, result.StoreId);
        Assert.Equal("R-100", result.ReceiptId);
        Assert.Equal(2.50m, result.Price);

        // points = (int)(price*100) => 250
        Assert.Equal(250, result.Points);
        Assert.Equal(250, result.NewTotalPoints);

        // confirm transaction persisted
        Assert.True(db.Transactions.Any(t => t.ReceiptId == "R-100"));

        _output.WriteLine("[PASS] ProcessTransactionAsync_HappyPath_CreatesTransactionAndAddsPoints");
    }

    [Fact]
    public async Task ProcessTransactionAsync_DuplicateReceipt_Throws()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var mallId = Guid.NewGuid();
        var store = new Store { Id = Guid.NewGuid(), Name = "S1", MallID = mallId };
        db.Stores.Add(store);

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            MallID = mallId,
            PhoneNumber = "+962791234567",
            TotalPoints = 0
        };
        db.UserProfiles.Add(user);

        // existing transaction with same receiptId
        db.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            StoreId = store.Id,
            ReceiptId = "R-dup",
            ReceiptDescription = "",
            Price = 1m,
            Points = 100,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProcessTransactionAsync("+962791234567", store.Id, "R-dup", null, 1m)
        );

        Assert.Equal("Receipt ID already exists", ex.Message);
        _output.WriteLine("[PASS] ProcessTransactionAsync_DuplicateReceipt_Throws");
    }

    [Fact]
    public async Task ProcessTransactionAsync_UserNotFound_Throws()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var mallId = Guid.NewGuid();
        var store = new Store { Id = Guid.NewGuid(), Name = "S1", MallID = mallId };
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ProcessTransactionAsync("+962700000000", store.Id, "R-1", null, 1m)
        );

        Assert.Equal("User not found", ex.Message);
        _output.WriteLine("[PASS] ProcessTransactionAsync_UserNotFound_Throws");
    }
}
