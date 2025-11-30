/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a package
 * As represented in cobertura XML
 *
 * PackageCoverage.cs
 */

using System.Xml.Serialization;

namespace TestMap.Models.Coverage;

public class PackageCoverage
{
    [XmlAttribute("line-rate")] public double LineRate { get; set; } = 0.0;

    [XmlAttribute("branch-rate")] public double BranchRate { get; set; } = 0.0;

    [XmlAttribute("complexity")] public int Complexity { get; set; } = 0;

    [XmlAttribute("name")] public string Name { get; set; } = "";

    [XmlArray("classes")]
    [XmlArrayItem("class")]
    public List<ClassCoverage> Classes { get; set; } = new();
}