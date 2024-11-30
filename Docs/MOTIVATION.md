# Motivation

Primary motivation for this work comes from a few sources.

1. [PEX](https://www.microsoft.com/en-us/research/publication/pex-white-box-test-generation-for-net/?msockid=2f09d6198b8962673611c35e8a8e63fd)
2. [Unit Test Case Generation with Transformers and Focal Context](https://www.microsoft.com/en-us/research/publication/unit-test-case-generation-with-transformers-and-focal-context/?msockid=2f09d6198b8962673611c35e8a8e63fd)


## 1. PEX

PEX was an automated software testing tool for CSharp. PEX eventually became [IntelliTest](https://learn.microsoft.com/en-us/visualstudio/test/generate-unit-tests-for-your-code-with-intellitest?view=vs-2022) which 
is integrated into Visual Studio. Since then, most research has largely focused on other programming languages. 
Inspired by this tool and the overall lack of CSharp in testing research, we decided that CSharp would be the perfect extension to the second reference.

## 2. Unit Test Case Generation with Transformers and Focal Context

Mostly this explores collecting testing data from Java repositories and training a transformer model
to generate unit tests. They compare this approach against a traditional automated testing tool, [EvoSuite](https://github.com/EvoSuite/evosuite),
and GPT-3. They see that their approach outperforms the existing state-of-the-art approaches. There
are many papers now that explore this type of approach. We were interested in extending this approach to CSharp
and comparing it to a traditional automated testing tool such as PEX. However, we couldn't find
any datasets that contained CSharp test methods mapped to their "source code (focal context, i.e, the code being tested)" so
we decided to create a tool that could collect this data.