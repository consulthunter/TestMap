# Testing

Testing for TestMap is pretty simple currently.

We have one testing project, ```TestMap.Tests```

```TestMap.Tests``` has ```UnitTests``` and ```IntegrationTests```

Most of the unit tests are run through GitHub Actions,

The integration tests should be run locally, though eventually could be modified to run through CI.

Testing Frameworks used:
- ```xUnit```
- ```Moq```

## Unit Tests

Unit tests cover most of the program and we used mocking here.

Most of these tests can be run through the CI and are labeled with:
- ```[Trait("Category", "CI")]```


## Integration Tests

Integration tests actually do a full run of the program using the example repository, [TestMap-Example](https://github.com/consulthunter/TestMap-Example)

Since we currently use filepaths and other information, we suggest running these locally as they will likely fail in GitHub Actions.

Integration tests and other tests that need to be run locally are labeled:
- ```[Trait("Category", "Local")]```