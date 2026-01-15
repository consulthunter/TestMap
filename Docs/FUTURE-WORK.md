# Future Work

We have several ideas on how to extend this tool and ideas on how to improve the tool.


## Extensions

Possible extensions to the tool.

### Test Classification

One idea is to do test classification on tests that are retrieved in the analysis. 

For instance:
- Is it a unit test?
- Is it a integration test?
- Is it a system test?

Figuring out a method to determining the type of method would be particularly beneficial. We see this as being implemented as
another service that could be added to the project model. It should be added during the ```AnalyzeProjectService```

A couple of directions to start exploring:
- Traits for test methods
- Syntax trees
- Namespaces
- Keywords

Likely, all of these will need to be used when determining the type of test.

### Test Smells

Another idea is to detect test smells by integrating something like [xNose](https://github.com/tonoy30/xNose).

xNose is a tool that can detect test smells for xUnit tests.

Since xNose is written in C#, we could potentially use it as a library or integrate it into the tool as a service.

We could also extend xNose to additional test frameworks or test smells.