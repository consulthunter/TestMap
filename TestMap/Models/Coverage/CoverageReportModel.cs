/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage Report for a testing project
 * As represented in cobertura XML
 *
 * CoverageReport.cs
 */

using System.Globalization;
using System.Xml.Serialization;

namespace TestMap.Models.Coverage;

[XmlRoot("coverage")]
public class CoverageReportModel
{
    public const string CorrectedPolicyVersion = "coverage-integrity-v1";
    public const string LegacyCollectionStatus = "LegacyNotMeasured";

    [XmlAttribute("line-rate")] public double LineRate { get; set; } = 0.0;

    [XmlAttribute("branch-rate")] public double BranchRate { get; set; } = 0.0;

    [XmlAttribute("complexity")] public string ComplexityRaw { get; set; } = "0";

    [XmlIgnore]
    public double ComplexityValue =>
        double.TryParse(ComplexityRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)
            ? val
            : 0.0;

    [XmlAttribute("version")] public string Version { get; set; } = "";

    [XmlAttribute("timestamp")] public long Timestamp { get; set; } = 0;

    [XmlAttribute("lines-covered")] public int LinesCovered { get; set; } = 0;

    [XmlAttribute("lines-valid")] public int LinesValid { get; set; } = 0;

    [XmlAttribute("branches-covered")] public int BranchesCovered { get; set; } = 0;

    [XmlAttribute("branches-valid")] public int BranchesValid { get; set; } = 0;

    [XmlIgnore] public string RunId { get; set; } = string.Empty;

    [XmlIgnore] public string CollectionStatus { get; set; } = LegacyCollectionStatus;

    [XmlIgnore] public string CollectionReason { get; set; } = string.Empty;

    [XmlIgnore] public string SuccessfulCollector { get; set; } = string.Empty;

    [XmlIgnore] public string CollectionMetadataJson { get; set; } = string.Empty;

    [XmlIgnore] public bool HasUsableCoverage { get; set; }

    [XmlIgnore] public bool LineCountsAvailable { get; set; }

    [XmlIgnore] public bool BranchCountsAvailable { get; set; }

    [XmlIgnore] public string MeasurementPolicyVersion { get; set; } = string.Empty;

    [XmlIgnore] public int RawObjectCount { get; set; }

    [XmlIgnore] public int MappedObjectCount { get; set; }

    [XmlIgnore] public int RawMemberCount { get; set; }

    [XmlIgnore] public int MappedMemberCount { get; set; }

    [XmlIgnore] public string ScopeKind { get; set; } = "Solution";
    [XmlIgnore] public string ReportRole { get; set; } = Testing.TestReportRole.RepositoryBaseline;
    [XmlIgnore] public int? ExperimentRunId { get; set; }
    [XmlIgnore] public string SourceProjectPath { get; set; } = string.Empty;
    [XmlIgnore] public string TestProjectPath { get; set; } = string.Empty;
    [XmlIgnore] public string TargetFramework { get; set; } = string.Empty;

    [XmlArray("packages")]
    [XmlArrayItem("package")]
    public List<PackageCoverage> Packages { get; set; } = new();
}
