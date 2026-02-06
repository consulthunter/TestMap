using System.Linq;
using TestMap.Services.xNose.Walkers;

namespace TestMap.Services.xNose.Smells
{
    public class RedundantAssertionTestSmell : ASmell
    {
        public override bool HasSmell()
        {
            var root = GetRoot();
            var methodBodyWalker = new MethodBodyWalker();
            methodBodyWalker.Visit(root);
            return methodBodyWalker.Invocations.Select(i => i.ArgumentList).Any(argument =>
            {
                if (argument.Arguments.Count > 1)
                {
                    var expected = argument.Arguments[0].ToFullString();
                    var actual = argument.Arguments[1].ToFullString();
                    return string.Equals(expected, actual);
                }
                return false;
            });

        }

        public override string Name()
        {
            return nameof(RedundantAssertionTestSmell);
        }
    }
}