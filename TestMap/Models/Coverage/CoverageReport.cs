/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage Report for a testing project
 * As represented in cobertura XML
 *
 * CoverageReport.cs
 */

using System.Xml.Serialization;

namespace TestMap.Models.Coverage;

[XmlRoot("coverage")]
public class CoverageReport
{
        [XmlAttribute("line-rate")]
        public double LineRate { get; set; } =  0.0;

        [XmlAttribute("branch-rate")]
        public double BranchRate { get; set; } = 0.0;

        [XmlAttribute("complexity")] public int Complexity { get; set; } = 0;

        [XmlAttribute("version")] public string Version { get; set; } = "";

        [XmlAttribute("timestamp")] public long Timestamp { get; set; } = 0;

        [XmlAttribute("lines-covered")] public int LinesCovered { get; set; } = 0;

        [XmlAttribute("lines-valid")] public int LinesValid { get; set; } = 0;

        [XmlAttribute("branches-covered")] public int BranchesCovered { get; set; } = 0;

        [XmlAttribute("branches-valid")] public int BranchesValid { get; set; } = 0;

        [XmlArray("packages")]
        [XmlArrayItem("package")]
        public List<PackageCoverage> Packages { get; set; } = new List<PackageCoverage>();
}