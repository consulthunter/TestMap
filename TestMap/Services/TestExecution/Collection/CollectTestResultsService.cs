using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TestMap.App;
using TestMap.Models.Results;
using TestMap.Persistence.Ef;

namespace TestMap.Services.TestExecution.Collection;

public class CollectTestResultsService(ProjectContext context, TestMapDbContext dbContext)
{
    public async Task<(List<TestResultModel> Results, string Raw)> CollectAsync(string runId, string runDate)
    {
        var results = new List<TestResultModel>();
        var trxDir = Path.Combine(context.Project.DirectoryPath!, "coverage");

        if (!Directory.Exists(trxDir))
        {
            context.Project.Logger?.Warning($"TRX results directory not found: {trxDir}");
            return (results, string.Empty);
        }

        var testMemberIndex = await LoadTestMemberIndexAsync();
        var rawResults = new List<string>();
        var trxFiles = Directory.GetFiles(trxDir, "*.trx");

        foreach (var trxFile in trxFiles)
        {
            context.Project.Logger?.Information($"Parsing TRX file: {trxFile}");
            var (fileResults, raw) = ParseTrxFile(trxFile, runId, runDate, testMemberIndex);
            results.AddRange(fileResults);

            if (!string.IsNullOrWhiteSpace(raw)) rawResults.Add(raw);
        }

        return (results, string.Join(Environment.NewLine, rawResults));
    }

    private static (List<TestResultModel> Results, string Raw) ParseTrxFile(string trxFilePath, string runId,
        string runDate,
        TestMemberIndex testMemberIndex)
    {
        var results = new List<TestResultModel>();
        var doc = XDocument.Load(trxFilePath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
        var testDefinitions = ParseTestDefinitions(doc, ns);

        var unitTestResults = doc.Descendants(ns + "UnitTestResult");

        foreach (var result in unitTestResults)
        {
            var testName = (string?)result.Attribute("testName") ?? "";
            var testId = (string?)result.Attribute("testId") ?? "";
            var executionId = (string?)result.Attribute("executionId") ?? "";
            var outcome = (string?)result.Attribute("outcome") ?? "";
            var durationStr = (string?)result.Attribute("duration") ?? "00:00:00";
            TimeSpan.TryParse(durationStr, out var duration);
            var methodIdentity = ResolveTestMethodIdentity(
                testName,
                testId,
                executionId,
                testDefinitions);

            var errorMessage = result
                .Element(ns + "Output")
                ?.Element(ns + "ErrorInfo")
                ?.Element(ns + "Message")
                ?.Value;

            results.Add(new TestResultModel
            {
                RunId = runId,
                RunDate = runDate,
                MethodId = testMemberIndex.Resolve(methodIdentity),
                TestName = testName,
                Outcome = outcome,
                Duration = duration,
                ErrorMessage = errorMessage
            });
        }

        return (results, doc.ToString());
    }

    private async Task<TestMemberIndex> LoadTestMemberIndexAsync()
    {
        var rows = await (
                from member in dbContext.Members.AsNoTracking()
                join obj in dbContext.Objects.AsNoTracking() on member.ObjectEntityId equals obj.Id
                where member.IsTestMember
                select new TestMemberRow(
                    member.Id,
                    member.Name,
                    obj.Namespace,
                    obj.Name))
            .ToListAsync();

        return new TestMemberIndex(rows);
    }

    private static Dictionary<string, TestMethodIdentity> ParseTestDefinitions(XDocument doc, XNamespace ns)
    {
        var definitions = new Dictionary<string, TestMethodIdentity>(StringComparer.OrdinalIgnoreCase);

        foreach (var unitTest in doc.Descendants(ns + "UnitTest"))
        {
            var testId = (string?)unitTest.Attribute("id") ?? string.Empty;
            var executionId = (string?)unitTest.Element(ns + "Execution")?.Attribute("id") ?? string.Empty;
            var testMethod = unitTest.Element(ns + "TestMethod");
            var className = (string?)testMethod?.Attribute("className") ?? string.Empty;
            var methodName = (string?)testMethod?.Attribute("name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className) || string.IsNullOrWhiteSpace(methodName)) continue;

            var identity = new TestMethodIdentity(className, methodName);
            if (!string.IsNullOrWhiteSpace(testId)) definitions[testId] = identity;
            if (!string.IsNullOrWhiteSpace(executionId)) definitions[executionId] = identity;
        }

        return definitions;
    }

    private static TestMethodIdentity ResolveTestMethodIdentity(
        string testName,
        string testId,
        string executionId,
        IReadOnlyDictionary<string, TestMethodIdentity> testDefinitions)
    {
        if (!string.IsNullOrWhiteSpace(testId) && testDefinitions.TryGetValue(testId, out var byTestId))
            return byTestId;

        if (!string.IsNullOrWhiteSpace(executionId) && testDefinitions.TryGetValue(executionId, out var byExecutionId))
            return byExecutionId;

        var displayName = StripDisplayArguments(testName);
        var lastDot = displayName.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == displayName.Length - 1)
            return new TestMethodIdentity(string.Empty, displayName);

        return new TestMethodIdentity(displayName[..lastDot], displayName[(lastDot + 1)..]);
    }

    private static string StripDisplayArguments(string testName)
    {
        var argumentIndex = testName.IndexOf('(');
        return (argumentIndex >= 0 ? testName[..argumentIndex] : testName).Trim();
    }

    private sealed class TestMemberIndex
    {
        private readonly Dictionary<string, int> _byQualifiedName;
        private readonly Dictionary<string, int> _byClassAndMethod;
        private readonly Dictionary<string, int> _byMethodName;

        public TestMemberIndex(IReadOnlyCollection<TestMemberRow> rows)
        {
            _byQualifiedName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _byClassAndMethod = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            _byMethodName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var qualifiedClassName = BuildQualifiedClassName(row.Namespace, row.ClassName);
                AddUnique(_byQualifiedName, $"{qualifiedClassName}.{row.MethodName}", row.MemberId);
                AddUnique(_byClassAndMethod, $"{NormalizeClassName(qualifiedClassName)}.{row.MethodName}", row.MemberId);
                AddUnique(_byMethodName, row.MethodName, row.MemberId);
            }
        }

        public int Resolve(TestMethodIdentity identity)
        {
            if (string.IsNullOrWhiteSpace(identity.MethodName)) return 0;

            var className = NormalizeClassName(identity.ClassName);
            if (!string.IsNullOrWhiteSpace(className))
            {
                var qualifiedKey = $"{className}.{identity.MethodName}";
                if (_byQualifiedName.TryGetValue(qualifiedKey, out var byQualifiedName)) return byQualifiedName;
                if (_byClassAndMethod.TryGetValue(qualifiedKey, out var byClassAndMethod)) return byClassAndMethod;
            }

            return _byMethodName.TryGetValue(identity.MethodName, out var byMethodName)
                ? byMethodName
                : 0;
        }

        private static void AddUnique(IDictionary<string, int> map, string key, int memberId)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            if (map.ContainsKey(key))
            {
                map[key] = 0;
                return;
            }

            map[key] = memberId;
        }

        private static string BuildQualifiedClassName(string ns, string className)
        {
            return string.IsNullOrWhiteSpace(ns) ? className : $"{ns}.{className}";
        }

        private static string NormalizeClassName(string className)
        {
            return className.Replace('+', '.').Trim();
        }
    }

    private sealed record TestMemberRow(int MemberId, string MethodName, string Namespace, string ClassName);

    private sealed record TestMethodIdentity(string ClassName, string MethodName);
}
