using Pipelinez.Testing.ApiApproval;
using Xunit;

namespace Pipelinez.AmazonS3.Tests;

public class ApiApprovalTests
{
    private const string UpdateApiBaselinesEnvironmentVariable = "PIPELINEZ_UPDATE_API_BASELINES";

    [Fact]
    public void Pipelinez_AmazonS3_Public_Api_Matches_Approved_Baseline()
    {
        var approvedPath = GetApprovedPath();
        var actual = ApiApprovalTextGenerator.Generate(typeof(AmazonS3PipelineBuilderExtensions).Assembly);
        if (ShouldUpdateApiBaselines())
        {
            File.WriteAllText(approvedPath, actual.Replace("\n", Environment.NewLine));
        }

        var approved = File.ReadAllText(approvedPath).Replace("\r\n", "\n");

        Assert.Equal(approved, actual);
    }

    private static bool ShouldUpdateApiBaselines()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(UpdateApiBaselinesEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
    }

    private static string GetApprovedPath()
    {
        var projectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        return Path.Combine(projectDirectory, "ApprovedApi", "Pipelinez.AmazonS3.publicapi.txt");
    }
}
