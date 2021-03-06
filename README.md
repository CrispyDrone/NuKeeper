# NuKeeper

Automagically update NuGet packages in all .NET projects.

This fork includes additional features and fixes that have not been integrated in the main repository of NuKeeper at the time of writing. This fork is only intended to temporarily bridge this gap. As soon as NuKeeper provides the same benefits, this fork can be deleted.

Additional features:

+ Support `--reviewer` for TFS for all commands that create pull requests
+ Support `--maxopenpullrequests` for AzureDevops/TFS for all commands that create pull requests
+ Support custom templates for commit messages, pull request title, and pull request body. The templates can be specified in [mustache](https://mustache.github.io/), we use the [Stubble engine](https://github.com/StubbleOrg/Stubble).
+ Support `--targetBranch` for Azure Devops while using the repository command and providing a remote url

+ ~~Support `--targetBranch` for TFS while using the repository command and providing a remote url~~ Now included in nukeeper.
+ ~~Fixed issue with `--change minor` or `--change patch` resulting in incorrect major or minor updates when multiple versions installed in different projects already differ in minor or major versions.~~ Now included in nukeeper.

## How To Build and Run From Source

You can install the nukeeper dotnet tool of current build using the `InstallNuKeeperDotNetTool` (.bat for Windows, .sh for macOS and Linux) found in the root of the repository.

>Note: this overrides your existing global installation of the NuKeeper dotnet tool.

You can build and package the tool using the following commands. The instructions assume that you are in the root of the repository.

```console
dotnet pack .\NuKeeper\NuKeeper.csproj -o ".\artifacts"
dotnet tool install nukeeper --global --add-source ".\artifacts"
nukeeper --version
```

> Note: On macOS and Linux, `.\NuKeeper\NuKeeper.csproj` and `.\artifacts` will need be switched to `./NuKeeper/NuKeeper.csproj` and `./artifacts` to accommodate for the different slash directions.

## Licensing

NuKeeper is licensed under the [Apache License](http://opensource.org/licenses/apache.html)

* Git automation by [LibGit2Sharp](https://github.com/libgit2/libgit2sharp/) licensed under MIT  
* Github client by [Octokit](https://github.com/octokit/octokit.net) licensed under MIT  
* NuGet protocol [NuGet.Protocol](https://github.com/NuGet/NuGet.Client) licensed under Apache License Version 2.0
* NuGet CommandLine [NuGet commandLine](https://github.com/NuGet/NuGet.Client) licensed under Apache License Version 2.0
* Command line parsing by [McMaster.Extensions.CommandLineUtils](https://github.com/natemcmaster/CommandLineUtils) licensed under Apache License Version 2.0

## Acknowledgements

<p align="center">
  <img src="https://github.com/NuKeeperDotNet/NuKeeper/blob/master/assets/Footer.svg" />
</p>
