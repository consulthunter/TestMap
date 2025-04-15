/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a class
 * As represented in cobertura XML
 *
 * ClassCoverage.cs
 */


using System.Xml.Serialization;

namespace TestMap.Models.Coverage;


public class ClassCoverage
{
    [XmlAttribute("line-rate")] public double LineRate { get; set; } = 0.0;

    [XmlAttribute("branch-rate")]
    public double BranchRate { get; set; }  = 0.0;

    [XmlAttribute("complexity")] public int Complexity { get; set; } = 1;

    [XmlAttribute("name")] public string Name { get; set; } = "";

    [XmlAttribute("filename")] public string Filename { get; set; } = "";

    [XmlArray("methods")]
    [XmlArrayItem("method")]
    public List<MethodCoverage> Methods { get; set; } =  new List<MethodCoverage>();

    [XmlArray("lines")]
    [XmlArrayItem("line")]
    public List<LineCoverage> Lines { get; set; } =   new List<LineCoverage>();
}