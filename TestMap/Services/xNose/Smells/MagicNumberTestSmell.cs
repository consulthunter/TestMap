using System.Linq;
using TestMap.Services.xNose.Walkers;

namespace TestMap.Services.xNose.Smells
{
    public class MagicNumberTestSmell : ASmell
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
                    var expected = int.TryParse(argument.Arguments[0].ToString(), out var _);
                    var actual = int.TryParse(argument.Arguments[1].ToString(), out var _);
                    return (expected && !actual) || (!expected && actual);
                }
                return false;
            });
        }

        public override string Name()
        {
            return nameof(MagicNumberTestSmell);
        }
    }
}