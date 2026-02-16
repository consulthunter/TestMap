using System;
using System.Linq;
using TestMap.Services.xNose.Walkers;

namespace TestMap.Services.xNose.Smells
{
    public class RedundantPrintTestSmell : ASmell
    {

        public override bool HasSmell()
        {
            var root = GetRoot();
            var methodWalker = new MethodBodyWalker();
            methodWalker.Visit(root);
            return methodWalker.Expressions
            .Any(ex => ex.Contains("console.write", StringComparison.InvariantCultureIgnoreCase));
        }

        public override string Name()
        {
            return nameof(RedundantPrintTestSmell);
        }
    }
}

