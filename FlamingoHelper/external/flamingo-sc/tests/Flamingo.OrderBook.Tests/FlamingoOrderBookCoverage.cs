using Neo.SmartContract.Testing.Coverage;
using Neo.SmartContract.Testing.Coverage.Formats;

namespace Flamingo.OrderBook.Tests;

/// <summary>
/// dotnet .\nccs.dll "C:\x\OrderBook\src\OrderBook" --generate-artifacts all -d
/// </summary>
[TestClass]
public class FlamingoOrderBookCoverage
{
    /// <summary>
    /// Required coverage to be success
    /// </summary>
    public static decimal RequiredCoverage { get; set; } = 0.00M;

    [AssemblyCleanup]
    public static void EnsureCoverage()
    {
        // // Join here all of your Coverage sources
        //
        // var coverage = OrderBookV2Tests.Coverage;
        // // coverage?.Join(OwnableTests.Coverage);
        //
        // // Ensure that the coverage is more than X% at the end of the tests
        //
        // Assert.IsNotNull(coverage);
        // Console.WriteLine(coverage.Dump());
        //
        // File.WriteAllText("coverage.instruction.html", coverage.Dump(DumpFormat.Html));
        //
        // if (NeoDebugInfo.TryLoad("TestingArtifacts/Flamingo.Broker.nefdbgnfo", out var dbg))
        // {
        //     File.WriteAllText("coverage.cobertura.xml", new CoberturaFormat((coverage, dbg)).Dump());
        //     CoverageReporting.CreateReport("coverage.cobertura.xml", "./coverageReport/", "x");
        // }
        //
        // Assert.IsTrue(coverage.CoveredLinesPercentage >= RequiredCoverage, $"Coverage is less than {RequiredCoverage:P2}");
    }
}
