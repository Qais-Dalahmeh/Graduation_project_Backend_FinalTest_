using Graduation_Project_Backend.Service;
using Graduation_Project_Backend.Tests.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Graduation_Project_Backend.Tests.Service;

public class NormalizePhoneTests
{
    private readonly ITestOutputHelper _output;

    public NormalizePhoneTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("0791234567", "+962791234567")]
    [InlineData("07 9123 4567", "+962791234567")]
    [InlineData("07-9123-4567", "+962791234567")]
    [InlineData("(079) 123-4567", "+962791234567")]
    [InlineData("962791234567", "+962791234567")]
    [InlineData("+962791234567", "+962791234567")]
    public void NormalizePhone_ValidJordanianFormats_ReturnsCanonical(string input, string expected)
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        var result = svc.NormalizePhone(input);

        _output.WriteLine($"Input: '{input}' => Result: '{result}', Expected: '{expected}'");
        Assert.Equal(expected, result);
        _output.WriteLine("[PASS] NormalizePhone_ValidJordanianFormats_ReturnsCanonical for input: '" + input + "'");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void NormalizePhone_Empty_Throws(string input)
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        _output.WriteLine($"Input (empty test): '{input.Replace("\n","\\n")}'");
        Assert.Throws<ArgumentException>(() => svc.NormalizePhone(input));
        _output.WriteLine("[PASS] NormalizePhone_Empty_Throws for input length " + input.Length);
    }


    [Theory]
    [InlineData("abc")]
    [InlineData("07A1234567")]
    [InlineData("+111791234567")]
    [InlineData("+962612345678")] // not mobile (not +9627...)
    [InlineData("0612345678")]    // not 07...
    public void NormalizePhone_InvalidFormats_Throws(string input)
    {
        using var db = DbFactory.CreateInMemoryDb();
        var svc = new ServiceClass(db);

        _output.WriteLine($"Invalid input test: '{input}'");
        Assert.Throws<ArgumentException>(() => svc.NormalizePhone(input));
        _output.WriteLine("[PASS] NormalizePhone_InvalidFormats_Throws for input: '" + input + "'");
    }
}
