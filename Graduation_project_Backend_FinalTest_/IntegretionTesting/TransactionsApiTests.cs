using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.DTOs;
using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;

namespace Graduation_Project_Backend.IntegrationTests;

[Collection("db")]
public class TransactionsApiTests
{
    private readonly DatabaseFixture _fx;
    private static readonly PasswordHasher<UserProfile> Hasher = new();

    public TransactionsApiTests(DatabaseFixture fx) => _fx = fx;

    private async Task<Guid> SeedUserAsync(string rawPhone, Guid mallId, string password = "pass")
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ServiceClass>();

        var normalized = service.NormalizePhone(rawPhone);

        var user = new UserProfile
        {
            Id = Guid.NewGuid(),
            PhoneNumber = normalized,
            Name = "Buyer",
            Role = "user",
            TotalPoints = 0,
            MallID = mallId
        };
        user.PasswordHash = Hasher.HashPassword(user, password);

        db.UserProfiles.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task SeedStoreAsync(Guid storeId)
    {
        using var scope = _fx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        
        db.Stores.Add(new Store
        {
            Id = storeId,
            Name = "Store Test"
        });

        await db.SaveChangesAsync();
    }

    // -------------------------
    // Controller validations
    // -------------------------

    [Fact]
    public async Task AddTransaction_NullBody_Returns400()
    {
        await _fx.ResetAsync();

        var res = await _fx.Client.PostAsJsonAsync<AddTransactionDto?>("/api/Transactions", null);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("validation errors")
            .And.Contain("One or more validation errors occurred");
    }


    [Fact]
    public async Task AddTransaction_NegativePrice_Returns400()
    {
        await _fx.ResetAsync();

        var dto = new AddTransactionDto
        {
            PhoneNumber = "0790000000",
            StoreId = Guid.NewGuid(),
            MallID = Guid.NewGuid(),
            ReceiptId = "R-1",
            Price = -1m
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Transactions", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("validation errors")
            .And.Contain("Price"); 

    }

    [Fact]
    public async Task AddTransaction_MissingPhone_Returns400()
    {
        await _fx.ResetAsync();

        var dto = new AddTransactionDto
        {
            PhoneNumber = "",
            StoreId = Guid.NewGuid(),
            MallID = Guid.NewGuid(),
            ReceiptId = "R-1",
            Price = 10m
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Transactions", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Phone number is required");
    }

    [Fact]
    public async Task AddTransaction_MissingReceiptId_Returns400()
    {
        await _fx.ResetAsync();

        var dto = new AddTransactionDto
        {
            PhoneNumber = "0790000000",
            StoreId = Guid.NewGuid(),
            MallID = Guid.NewGuid(),
            ReceiptId = "",
            Price = 10m
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Transactions", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Receipt ID is required");
    }


    // -------------------------
    // Happy path + GET
    // -------------------------
    [Fact]
    public async Task AddTransaction_Success_FinalFix()
    {
        
        await _fx.ResetAsync();

        var mallId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var phone = "+962791234567"; 

       
        using (var scope = _fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

          
            db.Stores.Add(new Store { Id = storeId, Name = "Test Store", MallID = mallId });

            
            db.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                PhoneNumber = phone,
                MallID = mallId,
                Name = "Test User",
                TotalPoints = 0
            });

            await db.SaveChangesAsync();
        }

        
        var dto = new AddTransactionDto
        {
            PhoneNumber = phone,
            StoreId = storeId,
            MallID = mallId,
            ReceiptId = $"REC-{Guid.NewGuid():N}",
            Price = 20.0m,
            CreatedAt = DateTimeOffset.UtcNow,
            MadeAt = DateTimeOffset.UtcNow     
        };

       
        var res = await _fx.Client.PostAsJsonAsync("/api/Transactions", dto);

     
        if (!res.IsSuccessStatusCode)
        {
            var error = await res.Content.ReadAsStringAsync();
            throw new Exception($"Test Failed! Error from API: {error}");
        }

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddTransaction_DuplicateReceiptId_Returns400()
    {
        await _fx.ResetAsync();

        var mallId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        await SeedStoreAsync(storeId);
        await SeedUserAsync("0795555555", mallId);

        var receiptId = $"R-{Guid.NewGuid():N}";

       
        using (var scope = _fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<ServiceClass>();

            var normalizedPhone = service.NormalizePhone("0795555555");
            var user = await db.UserProfiles.SingleAsync(u => u.PhoneNumber == normalizedPhone && u.MallID == mallId);

            db.Transactions.Add(new Transaction
            {
                UserId = user.Id,
                StoreId = storeId,
                ReceiptId = receiptId,
                ReceiptDescription = "seed receipt",  
                Price = 10m,
                Points = 5
            });

            await db.SaveChangesAsync();
        }

        var dto = new AddTransactionDto
        {
            PhoneNumber = "0795555555",
            StoreId = storeId,
            MallID = mallId,
            ReceiptId = receiptId,
            ReceiptDescription = "api receipt",
            Price = 10m
        };

        var res = await _fx.Client.PostAsJsonAsync("/api/Transactions", dto);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }


    [Fact]
    public async Task GetTransactionById_NotFound_Returns404()
    {
        await _fx.ResetAsync();

        var res = await _fx.Client.GetAsync("/api/Transactions/99999999");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var text = await res.Content.ReadAsStringAsync();
        text.Should().Contain("Transaction not found");
    }
}
