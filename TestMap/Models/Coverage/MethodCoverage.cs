/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a method
 * As represented in cobertura XML
 *
 * MethodCoverage.cs
 */

using System.Xml.Serialization;


namespace TestMap.Models.Coverage;

public class MethodCoverage
{
    [XmlAttribute("line-rate")]
    public double LineRate { get; set; } = 0.0;

    [XmlAttribute("branch-rate")]
    public double BranchRate { get; set; } = 0.0;

    [XmlAttribute("complexity")] public int Complexity { get; set; } = 0;

    [XmlAttribute("name")] public string Name { get; set; } = "";

    [XmlAttribute("signature")] public string Signature { get; set; } = "";

    [XmlArray("lines")]
    [XmlArrayItem("line")]
    public List<LineCoverage> Lines { get; set; } =  new List<LineCoverage>();
}