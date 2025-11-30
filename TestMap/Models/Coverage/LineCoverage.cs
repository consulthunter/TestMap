/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a line of code
 * As represented in cobertura XML
 *
 * LineCoverage.cs
 */

using System.Xml.Serialization;

namespace TestMap.Models.Coverage;

public class LineCoverage
{
    [XmlAttribute("number")] public int Number { get; set; } = 0;

    [XmlAttribute("hits")] public int Hits { get; set; } = 0;

    [XmlAttribute("branch")] public string Branch { get; set; } = "";

    [XmlAttribute("condition-coverage")] public string ConditionCoverage { get; set; } = "";

    [XmlArray("conditions")]
    [XmlArrayItem("condition")]
    public List<ConditionCoverage> Conditions { get; set; } = new();
}