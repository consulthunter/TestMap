using System.Linq;
using System;
using TestMap.Services.xNose.Walkers;

namespace TestMap.Services.xNose.Smells
{
	public class EqualInAssertSmell : ASmell
    {

        public override bool HasSmell()
        {
            var root = GetRoot();
            var methodBodyWalker = new MethodBodyWalker();
            methodBodyWalker.Visit(root);
            var invocations = methodBodyWalker.Invocations
                 .Where(x => (x.Expression.ToString().Contains("Assert.False", StringComparison.InvariantCultureIgnoreCase) ||
                            x.Expression.ToString().Contains("Assert.True", StringComparison.InvariantCultureIgnoreCase)));

            foreach (var invocation in invocations)
            {
                if (invocation.ArgumentList.Arguments.ToString().Contains('='))
                    return true;
            }
            return false;
        }

        public override string Name()
        {
            return nameof(EqualInAssertSmell);
        }
    }
}

