using System;
using TestMap.Services.xNose.Walkers;

namespace TestMap.Services.xNose.Smells
{
	public class ObscureInLineSetUpSmell : ASmell
	{

        public override bool HasSmell()
        {
            var root = GetRoot();
            var methodWalker = new MethodBodyWalker();
            methodWalker.Visit(root);
            return methodWalker.LocalDeclarations.Count > 10;
        }

        public override string Name()
        {
            return nameof(ObscureInLineSetUpSmell);
        }
    }
}

