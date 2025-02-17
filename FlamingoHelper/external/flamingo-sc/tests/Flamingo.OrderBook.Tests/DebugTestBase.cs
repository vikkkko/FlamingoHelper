using System.Numerics;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Testing;
using Neo.SmartContract.Testing.Coverage;
using Neo.SmartContract.Testing.TestingStandards;
using System.IO;
using System.Collections.Generic;

namespace Flamingo.OrderBook.Tests;

public abstract class DebugTestBase<T> : TestBase<T> where T : SmartContract, IContractInfo
{
    protected DebugTestBase(NefFile nefFile, ContractManifest manifestFile, NeoDebugInfo? debugInfo = null)
        : base(nefFile, manifestFile, debugInfo)
    {
    }

    protected override TestEngine CreateTestEngine()
    {
        var testEngine = base.CreateTestEngine();
        testEngine.MethodDetection = MethodDetectionMechanism.NextMethod;
        return testEngine;
    }

    public class CoverageHitCollection
    {
        public List<CoverageHit> Hits { get; } = new();
        private BigInteger _subtractedGasCost = 0;

        public void Add(CoverageHit coverageHit)
        {
            Hits.Add(coverageHit);
        }

        public BigInteger TotalGasCost()
        {
            BigInteger totalGasCost = 0;
            foreach (var hit in Hits)
            {
                totalGasCost += hit.FeeTotal;
            }
            // totalGasCost -= _subtractedGasCost;
            return totalGasCost;
        }

        public string FullDescription()
        {
            var output = new StringWriter();
            foreach (var hit in Hits)
            {
                output.WriteLine(hit.Description);
            }
            return output.ToString();
        }

        public void SubtractGasCost(BigInteger gasCostToSubtract)
        {
            _subtractedGasCost += gasCostToSubtract;
        }

        public BigInteger TotalLineHits()
        {
            BigInteger totalLineHits = 0;
            foreach (var hit in Hits)
            {
                totalLineHits += hit.Hits;
            }
            return totalLineHits;
        }
    }

    public class DocumentsGasCostInfo
    {
        public Dictionary<int, Dictionary<int, CoverageHitCollection>> DocumentGasCostMap { get; } = new();

        public void SubtractCosts(DocumentsGasCostInfo? subtractFrom)
        {
            if (subtractFrom == null) return;

            foreach (var (document, lines) in this.DocumentGasCostMap)
            {
                if (!subtractFrom.DocumentGasCostMap.ContainsKey(document)) continue;

                foreach (var (line, _) in lines)
                {
                    if (subtractFrom.DocumentGasCostMap[document].ContainsKey(line))
                    {
                        DocumentGasCostMap[document][line].SubtractGasCost(subtractFrom.DocumentGasCostMap[document][line].TotalGasCost());
                    }
                }
            }
        }
    }

    private NeoDebugInfo.SequencePoint InstructionLineToSequencePoint(int instructionLine)
    {
        if (DebugInfo == null)
            throw new InvalidOperationException("DebugInfo is not available");

        foreach (var method in DebugInfo.Methods)
        {
            foreach (var sequencePoint in method.SequencePoints)
            {
                if (sequencePoint.Address == instructionLine)
                {
                    return sequencePoint;
                }
            }
        }
        throw new InvalidOperationException($"Instruction line {instructionLine} not found in debug info");
    }

    private void UpdateGasCostMap(Dictionary<int, Dictionary<int, CoverageHitCollection>> gasCostMap, int document, int line, CoverageHit coverageHit)
    {
        if (!gasCostMap.ContainsKey(document))
            gasCostMap[document] = new Dictionary<int, CoverageHitCollection>();

        if (!gasCostMap[document].ContainsKey(line))
            gasCostMap[document][line] = new CoverageHitCollection();

        gasCostMap[document][line].Add(coverageHit);
    }

    public DocumentsGasCostInfo GenerateDocumentGasCostMap(DocumentsGasCostInfo? subtractFrom = null)
    {
        var contractCoverage = Contract.GetCoverage();
        DocumentsGasCostInfo documentGasCostMap = new();

        BigInteger totalFee = 0;

        foreach (var method in contractCoverage!.Methods)
        {
            foreach (var line in method.Lines)
            {
                try
                {
                    var sequencePoint = InstructionLineToSequencePoint(line.Offset);
                    UpdateGasCostMap(documentGasCostMap.DocumentGasCostMap, sequencePoint.Document, sequencePoint.Start.Line, line);
                    totalFee += line.FeeTotal;
                }
                catch (InvalidOperationException)
                {
                    // Ignore lines that don't have a sequence point
                }
            }
        }

        Console.WriteLine($"Total gas cost: {(float)totalFee / 100_000_000f}");

        documentGasCostMap.SubtractCosts(subtractFrom);

        return documentGasCostMap;
    }

    public void WriteGasCostsToFile(string outputName, DocumentsGasCostInfo documentGasCostMap)
    {
        var debugOutputDirectory = $"../../../debug_output/{outputName}";

        foreach (var document in documentGasCostMap.DocumentGasCostMap)
        {
            var documentName = DebugInfo!.Documents[document.Key];
            // if (documentName.StartsWith("..")) continue;
            // Remove everything except the file name
            documentName = documentName.Substring(documentName.LastIndexOf('/') + 1);

            var documentFullPath = Path.Combine(DebugInfo.DocumentRoot, documentName);
            WriteDocumentGasCosts(debugOutputDirectory, documentFullPath, document.Value);
        }
    }

    private void WriteDocumentGasCosts(string outputDirectory, string documentFullPath, Dictionary<int, CoverageHitCollection> gasCosts)
    {
        using var reader = new StreamReader(documentFullPath);
        var output = new StringWriter();
        int lineNumber = 1;

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();

            if (gasCosts.ContainsKey(lineNumber))
            {
                var coverageHitCollection = gasCosts[lineNumber];
                var newLine = $"{coverageHitCollection.TotalGasCost().ToString().PadLeft(10)} [{coverageHitCollection.TotalLineHits().ToString().PadLeft(3)}] | {line}";
                // newLine += $"\n{coverageHitCollection.FullDescription()}";
                output.WriteLine(newLine);
            }
            else
            {
                var newLine = $"                 | {line}";
                output.WriteLine(newLine);
            }
            lineNumber++;
        }

        var outputFilePath = Path.Combine(outputDirectory, Path.GetFileName(documentFullPath) + ".txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
        File.WriteAllText(outputFilePath, output.ToString());
    }
}
