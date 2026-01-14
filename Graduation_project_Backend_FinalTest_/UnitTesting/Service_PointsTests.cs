using Graduation_Project_Backend.Models.User;
using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Tests.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Graduation_Project_Backend.Tests.Service;

public class PointsTests
{
    private readonly ITestOutputHelper _output;

    public PointsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AddPoints_IncreasesTotalPoints()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var user = new UserProfile { TotalPoints = 10 };

        svc.AddPoints(user, 5);

        _output.WriteLine($"Before: 10, Added: 5, After: {user.TotalPoints}");
        Assert.Equal(15, user.TotalPoints);
        _output.WriteLine("[PASS] AddPoints_IncreasesTotalPoints");
    }

    [Fact]
    public void DeductPoints_WhenEnoughPoints_Decreases()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var user = new UserProfile { TotalPoints = 20 };

        svc.DeductPoints(user, 7);

        _output.WriteLine($"Before: 20, Deducted: 7, After: {user.TotalPoints}");
        Assert.Equal(13, user.TotalPoints);
        _output.WriteLine("[PASS] DeductPoints_WhenEnoughPoints_Decreases");
    }

    [Fact]
    public void DeductPoints_WhenNotEnough_Throws()
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var user = new UserProfile { TotalPoints = 3 };

        _output.WriteLine($"Before: 3, AttemptDeduct: 10");
        Assert.Throws<InvalidOperationException>(() => svc.DeductPoints(user, 10));
        _output.WriteLine("[PASS] DeductPoints_WhenNotEnough_Throws");
    }
}
