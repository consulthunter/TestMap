/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a function
 * As represented in cobertura XML
 *
 * FunctionCoverage.cs
 */

using System.Xml.Serialization;

namespace TestMap.Models.Coverage;

public class FunctionCoverage
{
    [XmlElement("name")] public string Name { get; set; } = "";
    [XmlElement("signature")] public string Signature { get; set; } = "";
    [XmlElement("line-rate")] public string LineRate { get; set; } = "";
    [XmlElement("branch-rate")] public string BranchRate { get; set; } = "";
    [XmlElement("complexity")] public string Complexity { get; set; } = "";
}