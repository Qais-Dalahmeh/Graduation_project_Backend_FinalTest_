using Graduation_Project_Backend.Models.Entities;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Tests.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Graduation_Project_Backend.Tests.Service;

public class StoreTests
{
    private readonly ITestOutputHelper _output;

    public StoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateStoreAsync_ValidName_CreatesStore()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var store = await svc.CreateStoreAsync("  My Store  ");

        Assert.NotEqual(Guid.Empty, store.Id);
        Assert.Equal("My Store", store.Name);

        // Confirm saved
        var saved = await svc.GetStoreByIdAsync(store.Id);
        Assert.NotNull(saved);
        Assert.Equal("My Store", saved!.Name);

        _output.WriteLine($"Created store Id={store.Id}, Name='{store.Name}'");
        _output.WriteLine("[PASS] CreateStoreAsync_ValidName_CreatesStore");
    }

    [Fact]
    public async Task CreateStoreAsync_EmptyName_Throws()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateStoreAsync(" "));
        _output.WriteLine("[PASS] CreateStoreAsync_EmptyName_Throws");
    }
}
