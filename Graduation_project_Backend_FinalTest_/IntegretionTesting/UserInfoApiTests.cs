using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Graduation_Project_Backend.Data;
using Graduation_Project_Backend.Models.User;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Graduation_Project_Backend.IntegrationTests;

[Collection("db")]
public class UserInfoApiTests
{
    private readonly DatabaseFixture _fx;

    public UserInfoApiTests(DatabaseFixture fx) => _fx = fx;

    [Fact]
    public async Task GetUserPoints_UserNotFound_Returns404()
    {
        await _fx.ResetAsync();

        var res = await _fx.Client.GetAsync($"/api/UserInfo/points/{Guid.NewGuid()}");
        var body = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.NotFound, body);

        // {"message":"User not found"}
        body.Should().Contain("User not found");
    }

    [Fact]
    public async Task GetUserPoints_UserExists_Returns200_AndTotalPoints()
    {
        await _fx.ResetAsync();

        var userId = Guid.NewGuid();

        // Seed user with points
        using (var scope = _fx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.UserProfiles.Add(new UserProfile
            {
                Id = userId,
                Name = "Points User",
                PhoneNumber = "0795555555",
                PasswordHash = "hash",
                TotalPoints = 123,
                Role = "user"
               
            });

            await db.SaveChangesAsync();
        }

        var res = await _fx.Client.GetAsync($"/api/UserInfo/points/{userId}");
        var body = await res.Content.ReadAsStringAsync();

        res.StatusCode.Should().Be(HttpStatusCode.OK, body);

        // {"totalPoints":123}
        body.Should().Contain("totalPoints");
        body.Should().Contain("123");
    }
}
