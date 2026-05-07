using TestMap.Models.Results;
using TestMap.Persistence.Ef.Entities.Testing;

namespace TestMap.Persistence.Ef.Mappings;

public static class TestResultMappingExtensions
{
    public static TestResultModel ToDomain(this TestResultEntity entity)
    {
        return new TestResultModel
        {
            RunId = entity.RunId,
            RunDate = entity.RunDate,
            MethodId = entity.MethodId,
            TestName = entity.TestName,
            Outcome = entity.Outcome,
            Duration = entity.Duration,
            ErrorMessage = entity.ErrorMessage
        };
    }

    public static TestResultEntity ToEntity(this TestResultModel model, int testRunId)
    {
        return new TestResultEntity
        {
            TestRunId = testRunId,
            RunId = model.RunId,
            RunDate = model.RunDate,
            MethodId = model.MethodId,
            TestName = model.TestName,
            Outcome = model.Outcome,
            Duration = model.Duration,
            ErrorMessage = model.ErrorMessage
        };
    }
}