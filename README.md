# Brief Overview of Design Choices

## Testability
Putting all of the code into the console app's program.cs file is not conducive to supporting tests as expected in the assessment requirements. To ensure testability, breaking out of a static context so that dependencies can be mocked and injected was required. I split most of the work into a separate library for holding entities, leaving the main project folder just to host the main application itself.

## Separation of Concerns
I broke out the various API calls between two different interfaces so that the calling program would not need to know about how the order processor goes about notifying various services of its work and so that the order processor cannot inadvertently submit a request to retrieve the medical orders itself. Both services are backed by the same concretion but this could also be split if desired.

## 3rd Party Services
In order to support some variation of non-console based logging, I decided to use a third party library by [adams85](https://github.com/adams85/filelogger). I decided to use a third party application for this because using external services to support logging such as Azure Monitor Logs seemed excessive for this project and Microsoft does not strongly support file loggers. Their own documentation for the [FileLogger class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.logging.filelogger?view=msbuild-17-netcore) contains the following commentary:

>It's unfortunate that this is derived from ConsoleLogger, which is itself a facade; it makes things more complex -- for example, there is parameter parsing in this class, plus in BaseConsoleLogger. However we have to derive FileLogger from ConsoleLogger because it shipped that way in Whidbey.

So I went with a third party package.

## Style Changes
I made a few stylistic changes to reduce cyclomatic complexity (number of nested braces) by utilizing inline `using` statements and reducing redundancy by simplifying calls to `new()` when appropriate. I find `var` to be much cleaner than reiterating the qualified name of an object.

## Unit Tests
There are not nearly as many unit tests as I would like. However, for time reasons, I have decided to test only the processor because it is quite difficult to go about mocking up `HttpContext` and most of those calls are relatively simple. For true completion, there should be several more tests that go over numerous intentional failure cases, ensuring log messages are generated correctly, that exceptions are captured, etc. Additionally, there should be tests for the API which do take the time to work through mocking an `HttpContext`.
