using System.Linq;
using System;
using TestMap.Services.xNose.Walkers;

namespace TestMap.Services.xNose.Smells
{
    public class SleepyTestSmell : ASmell
    {
        public override bool HasSmell()
        {
            var root = GetRoot();
            var methodWalker = new MethodBodyWalker();
            methodWalker.Visit(root);
            return methodWalker.Expressions
            .Any(ex => ex.Contains("thread.sleep", StringComparison.InvariantCultureIgnoreCase));

        }

        public override string Name()
        {
            return nameof(SleepyTestSmell);
        }
    }
}