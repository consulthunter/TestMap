using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using TestMap.Models;
using TestMap.Services.ProjectOperations;
using Xunit;

namespace TestMap.Tests.Models;

[TestSubject(typeof(TestMap.Models.TestMap))]
public class TestMapTest
{
        private readonly TestConfigurationLoader _configurationLoader;
        private readonly Mock<CloneRepoService> _mockCloneRepoService;
        private readonly Mock<SdkManager> _mockSdkManager;
        private readonly Mock<BuildSolutionService> _mockBuildSolutionService;
        private readonly Mock<BuildProjectService> _mockBuildProjectService;
        private readonly Mock<AnalyzeProjectService> _mockAnalyzeProjectService;
        private readonly Mock<DeleteProjectService> _mockDeleteProjectService;
        private TestMap.Models.TestMap _testMap;

        public TestMapTest()
        {
            _configurationLoader = new TestConfigurationLoader();
            
            // Initialize mocks
            _mockCloneRepoService = new Mock<CloneRepoService>(MockBehavior.Strict, _configurationLoader.ProjectModel);
            _mockSdkManager = new Mock<SdkManager>(MockBehavior.Strict, _configurationLoader.ProjectModel);
            _mockBuildSolutionService = new Mock<BuildSolutionService>(MockBehavior.Strict, _configurationLoader.ProjectModel);
            _mockBuildProjectService = new Mock<BuildProjectService>(MockBehavior.Strict, _configurationLoader.ProjectModel);
            _mockAnalyzeProjectService = new Mock<AnalyzeProjectService>(MockBehavior.Strict, _configurationLoader.ProjectModel);
            _mockDeleteProjectService = new Mock<DeleteProjectService>(MockBehavior.Strict, _configurationLoader.ProjectModel);
            
            CreateTestMap();
        }

        private void CreateTestMap()
        {
            _testMap = new TestMap.Models.TestMap
            (
                _configurationLoader.ProjectModel, 
                _mockCloneRepoService.Object, 
                _mockSdkManager.Object,
                _mockBuildSolutionService.Object, 
                _mockBuildProjectService.Object,
                _mockAnalyzeProjectService.Object,
                _mockDeleteProjectService.Object
            );
        }

        [Fact]
        public async Task RunAsync_CallsExpectedMethods()
        {
            // Arrange
            _mockBuildSolutionService
                .Setup(service => service.BuildSolutionsAsync())
                .Returns(Task.CompletedTask)
                .Verifiable();

            _mockBuildProjectService
                .Setup(service => service.BuildProjectCompilation(It.IsAny<AnalysisProject>()))
                .Returns(CSharpCompilation.Create("test"))
                .Verifiable();
            
            _mockAnalyzeProjectService
                .Setup(service => service.AnalyzeProjectAsync(It.IsAny<AnalysisProject>(), It.IsAny<CSharpCompilation>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            await _testMap.RunAsync();

            // Assert
            _mockBuildSolutionService.Verify();
            _mockBuildProjectService.Verify();
            _mockAnalyzeProjectService.Verify();
        }
        
}