using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestMap.Services.xNose.Smells
{
	public class ConstructorInitializationTestSmell : ASmell
	{
        public override bool HasSmell()
        {
            var root = GetRoot();

            ClassDeclarationSyntax classDeclaration = root.DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .FirstOrDefault();
            if (classDeclaration != null)
            {
                ConstructorDeclarationSyntax constructor = classDeclaration.Members
                                                            .OfType<ConstructorDeclarationSyntax>()
                                                            .FirstOrDefault();
                return constructor != null;
            }
           
            return false;
        }

        public override string Name()
        {
            return nameof(ConstructorInitializationTestSmell);
        }
    }
}

