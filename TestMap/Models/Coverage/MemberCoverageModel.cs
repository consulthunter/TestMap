/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a method
 * As represented in cobertura XML
 *
 * MethodCoverage.cs
 */

using System.Globalization;
using System.Xml.Serialization;


namespace TestMap.Models.Coverage;

public class MemberCoverageModel
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

    [XmlAttribute("signature")] public string Signature { get; set; } = "";

    [XmlIgnore] public int? MemberId { get; set; }

    [XmlIgnore] public int CoverageReportId { get; set; }

    [XmlIgnore] public int? ObjectCoverageId { get; set; }

    [XmlIgnore] public int SourceOrdinal { get; set; } = -1;

    [XmlIgnore] public string AttributionStatus { get; set; } = string.Empty;

    [XmlIgnore] public string AttributionReason { get; set; } = string.Empty;

    [XmlIgnore] public int LinesCovered { get; set; }

    [XmlIgnore] public int LinesValid { get; set; }

    [XmlIgnore] public int BranchesCovered { get; set; }

    [XmlIgnore] public int BranchesValid { get; set; }

    [XmlIgnore] public bool LineCountsAvailable { get; set; }

    [XmlIgnore] public bool BranchCountsAvailable { get; set; }

    [XmlIgnore] public string CounterValidationError { get; set; } = string.Empty;

    [XmlArray("lines")]
    [XmlArrayItem("line")]
    public List<LineCoverageModel> Lines { get; set; } = new();
}
