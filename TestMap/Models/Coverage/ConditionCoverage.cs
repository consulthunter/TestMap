/*
 * consulthunter
 * 2025-04-09
 *
 * Coverage for a condition
 * As represented in cobertura XML
 *
 * ConditionCoverage.cs
 */

using System.Xml.Serialization;

namespace TestMap.Models.Coverage;

public class ConditionCoverage
{
    [XmlAttribute("number")] public int Number { get; set; } = 0;

    [XmlAttribute("type")] public string Type { get; set; } = "";

    [XmlAttribute("coverage")] public string Coverage { get; set; } = "";
}