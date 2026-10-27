using ComplianceChecker.Rules;
using Xunit;

namespace TavernOracle.ComplianceTests;

public class BannedPackageRuleTests
{
    private readonly BannedPackageRule _rule = new();

    [Fact]
    public void AnalyzeProject_WithMemoryDll_MustDetectViolation()
    {
        string projectXml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Memory.dll" Version="1.2.24" />
              </ItemGroup>
            </Project>
            """;

        var violations = _rule.AnalyzeProject("TestApp.csproj", projectXml).ToList();

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COMP003" && v.Message.Contains("Memory.dll"));
    }

    [Fact]
    public void AnalyzeProject_WithEasyHook_MustDetectViolation()
    {
        string projectXml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="EasyHook" Version="2.7.7097" />
              </ItemGroup>
            </Project>
            """;

        var violations = _rule.AnalyzeProject("TestApp.csproj", projectXml).ToList();

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COMP003" && v.Message.Contains("EasyHook"));
    }

    [Fact]
    public void AnalyzeProject_WithDirectInputSimulatorReference_MustDetectViolation()
    {
        string projectXml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <Reference Include="InputSimulator.dll" />
              </ItemGroup>
            </Project>
            """;

        var violations = _rule.AnalyzeProject("TestApp.csproj", projectXml).ToList();

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleId == "COMP003" && v.Message.Contains("InputSimulator"));
    }

    [Fact]
    public void AnalyzeProject_WithLegitimatePackages_MustPassAudit()
    {
        string projectXml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
                <PackageReference Include="xunit" Version="2.9.3" />
                <PackageReference Include="HearthDb" Version="28.0.0" />
              </ItemGroup>
            </Project>
            """;

        var violations = _rule.AnalyzeProject("TavernOracle.csproj", projectXml).ToList();

        Assert.Empty(violations);
    }
}
