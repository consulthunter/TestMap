namespace TestMap.UnitTests.Utilities;

/// <summary>
/// A generated test belongs to the attempt that produced it. These cover the member identity
/// that makes that true: two attempts emitting a same-named test with different bodies must get
/// different member ids, so the metrics and smells attached to each are never rewritten by the
/// other.
/// </summary>
public sealed class MemberIdentityHashTests
{
    private const string AddStudentV1 = """
                                        [TestMethod]
                                        public void TestStartAddStudent()
                                        {
                                            var writer = new StringWriter();
                                            Program.Start(new StringReader("1\n4\n"), writer);
                                            Assert.IsTrue(writer.ToString().Contains("Added"));
                                        }
                                        """;

    private const string AddStudentV2 = """
                                        [TestMethod]
                                        public void TestStartAddStudent()
                                        {
                                            var writer = new StringWriter();
                                            Program.Start(new StringReader("1\n1\n4\n"), writer);
                                            Assert.IsTrue(writer.ToString().Contains("Student"));
                                            Assert.IsTrue(writer.ToString().Contains("Goodbye"));
                                        }
                                        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeMemberIdentityHash_SameNameDifferentBody_AreDifferentMembers()
    {
        var first = Hash(AddStudentV1);
        var second = Hash(AddStudentV2);

        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeMemberIdentityHash_IdenticalBody_IsTheSameMember()
    {
        Assert.Equal(Hash(AddStudentV1), Hash(AddStudentV1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeMemberIdentityHash_ReindentedBody_IsTheSameMember()
    {
        // Inserting a test above this one shifts it; that is layout, not a new version.
        var reindented = string.Join(
            "\n",
            AddStudentV1.Split('\n').Select(line => "        " + line));

        Assert.Equal(Hash(AddStudentV1), Hash(reindented));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeMemberIdentityHash_DifferentDeclaringObject_AreDifferentMembers()
    {
        Assert.NotEqual(
            TestMap.Utilities.Utilities.ComputeMemberIdentityHash(1, "TestStartAddStudent", "method", AddStudentV1),
            TestMap.Utilities.Utilities.ComputeMemberIdentityHash(2, "TestStartAddStudent", "method", AddStudentV1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeMemberIdentityHash_SameBodyDifferentName_AreDifferentMembers()
    {
        var renamed = AddStudentV1.Replace("TestStartAddStudent", "TestStartRemoveStudent");

        Assert.NotEqual(
            TestMap.Utilities.Utilities.ComputeMemberIdentityHash(1, "TestStartAddStudent", "method", AddStudentV1),
            TestMap.Utilities.Utilities.ComputeMemberIdentityHash(1, "TestStartRemoveStudent", "method", renamed));
    }

    private static string Hash(string body) =>
        TestMap.Utilities.Utilities.ComputeMemberIdentityHash(1, "TestStartAddStudent", "method", body);
}
