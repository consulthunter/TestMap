/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a class
 * As represented in cobertura XML
 *
 * ClassCoverage.cs
 */


using System.Globalization;
using System.Xml.Serialization;

namespace TestMap.Models.Coverage;

public class ObjectCoverageModel
{
    [XmlAttribute("line-rate")] public double LineRate { get; set; } = 0.0;

    [XmlAttribute("branch-rate")] public double BranchRate { get; set; } = 0.0;

    [XmlAttribute("complexity")] public string ComplexityRaw { get; set; } = "0";

    [XmlIgnore]
    public double ComplexityValue =>
        double.TryParse(ComplexityRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)
            ? val
            : 0.0;

    [XmlAttribute("name")] public string Name { get; set; } = "";

    [XmlAttribute("filename")] public string Filename { get; set; } = "";

    [XmlIgnore] public int? ObjectId { get; set; }

    [XmlIgnore] public int CoverageReportId { get; set; }

    [XmlIgnore] public int SourceOrdinal { get; set; } = -1;

    [XmlIgnore] public string PackageName { get; set; } = string.Empty;

    [XmlIgnore] public string AttributionStatus { get; set; } = string.Empty;

    [XmlIgnore] public string AttributionReason { get; set; } = string.Empty;

    [XmlIgnore] public int LinesCovered { get; set; }

    [XmlIgnore] public int LinesValid { get; set; }

    [XmlIgnore] public int BranchesCovered { get; set; }

    [XmlIgnore] public int BranchesValid { get; set; }

    [XmlIgnore] public bool LineCountsAvailable { get; set; }

    [XmlIgnore] public bool BranchCountsAvailable { get; set; }

    [XmlIgnore] public string CounterValidationError { get; set; } = string.Empty;

    [XmlArray("methods")]
    [XmlArrayItem("method")]
    public List<MemberCoverageModel> Methods { get; set; } = new();

    [XmlArray("lines")]
    [XmlArrayItem("line")]
    public List<LineCoverageModel> Lines { get; set; } = new();
}
