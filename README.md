# A Microsoft.Extensions.AI wrapper for Gemini

> WARNING: this is a work in progress, it should not be used by anyone

The [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
library provides an abstraction for large language model (LLM) APIs. This library provides an implementation
of those abstractions defined in the
[Microsoft.Extensions.AI.Abstractions nuget package](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions)
by wrapping the Gemini API.

## TODO

* Fix all TODOs in the code.
* Add exception mapping. Currently we allow `RpcException` to bubble up. There might be something
  nicer we can do.
* Add an implementation of interfaces besides `IChatClient`, like `IEmbeddingGenerator`.
* Consider NativeAOT / trimming compatibility, if the underlying libraries make it possible.
* Adjust the target framework version to support the same frameworks as the Nuget packages we depend
  upon. If we add an in-support .NET Core target, we may want to take the same approach as Microsoft
  by emitting a warning on downlevel frameworks supported by .NET Standard 2.0.
  [See here](https://github.com/dotnet/runtime/blob/367865bf4540921ac4f16b404275e181698a2272/eng/packaging.targets#L209-L214).
