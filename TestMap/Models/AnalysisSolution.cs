using Microsoft.CodeAnalysis;

namespace TestMap.Models;

public class AnalysisSolution
{
    public Solution Solution;

    public List<string> Projects;

    public AnalysisSolution(Solution solution, List<string> projects)
    {
        Solution = solution;
        Projects = projects;
    }
}