# dotnet-xdt [![Build status](https://ci.appveyor.com/api/projects/status/559na9y3iswe9hbh/branch/master?svg=true)](https://ci.appveyor.com/project/nil4/dotnet-transform-xdt/branch/master)

Tools and library for applying [XML Document Transformations](https://msdn.microsoft.com/en-us/library/dd465326.aspx)
to e.g. .NET configuration files, or any other XML-structured content.

### <a name="dotnet-xdt-tool"></a> Global tool for .NET Core 2.1 and later  [![NuGet package](https://img.shields.io/nuget/dt/dotnet-xdt.svg)](https://www.nuget.org/packages/dotnet-xdt/) 

.NET Core 2.1 introduces the concept of [global tools](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools),
meaning that you can install `dotnet-xdt` using the .NET CLI and use it everywhere. One advantage of this approach 
is that you can use the same command, for both installation and usage, across all platforms.

> :warning: To use global tools, .Net Core SDK 2.1.300 or later is required. 

Install `dotnet-xdt` as a global tool (only once):

```cmd
dotnet tool install --global dotnet-xdt --version 2.1.0
```

And then you can apply XDT transforms, from the command-line, anywhere on your PC, e.g.:

```shell
dotnet xdt --source original.xml --transform delta.xml --output final.xml
```

Global tools are not ideal when an application needs to build completely self-contained,
without relying on tools installed in the surrounding environment. They are also only 
available on the latest .NET Core.

Read on if this is a concern for your application.

### <a name="dotnet-transform-xdt-tool"></a> Project tool for .NET Core 2.0 and earlier

.NET Core 2.0 and earlier do not support global tools. 

The "classic" version of this tool, however, *can* be installed as a 
[project-level tool](https://docs.microsoft.com/en-us/dotnet/core/tools/extensibility#per-project-based-extensibility)
on both the latest .NET Core, as well as on previous versions.

The tradeoff is that in this usage model, the tool can only be invoked 
via `dotnet transform-xdt`, and only from the folder of the project that references it. 

See the [project-level `dotnet-transform-xdt` tool](#legacy) section 
below for details. [A separate repository](https://github.com/nil4/xdt-samples/) provides a 
few self-contained sample projects that use `dotnet-transform-xdt` 
for Web.config transformations at publish time. 

### <a name="dotnet-xdt-exe"></a>Standalone executable for Windows

You can also download a standalone `dotnet-xdt.exe` that runs on any Windows PC with .NET 
Framework 4.6.1 installed. It has no external dependencies, nor does it require .NET Core.
It *might* run on Mono, but this scenario is not tested.

Download the latest build of `dotnet-xdt.exe` from the [releases page](https://github.com/nil4/dotnet-transform-xdt/releases).

### <a name="dotnet-xdt-lib"></a>.NET Standard 2.0 library [![NuGet package](https://img.shields.io/nuget/dt/DotNet.Xdt.svg)](https://www.nuget.org/packages/DotNet.Xdt/) 

For complete flexibility, reference the cross-platform `DotNet.Xdt` NuGet package in your application:

```cmd
dotnet add package DotNet.Xdt --version 2.1.0
```

You can apply XDT transforms to any XML file, or other XML sources that can be read from
and written to a .NET `Stream`. 

Define a class `MyXdtLogger` that implements `IXmlTransformationLogger`.
Then, apply transformations using code similar to:

```csharp
var document = new XmlTransformableDocument { PreserveWhitespace = true };

using (var sourceStream = File.OpenRead(sourceFilePath))
using (var transformStream = File.OpenRead(transformFilePath))
using (var transformation = new XmlTransformation(transformStream, new MyXdtLogger()))
{
    document.Load(sourceStream);
    transformation.Apply(document);
}

using (FileStream outputStream = File.Create(outputFilePath))
using (var outputWriter = XmlWriter.Create(outputStream, new XmlWriterSettings { Indent = true }))
{
    document.WriteTo(outputWriter);
}
```

## <a name="legacy"></a> Project-level `dotnet-transform-xdt` tool [![NuGet package](https://img.shields.io/nuget/dt/Microsoft.DotNet.Xdt.Tools.svg)](https://www.nuget.org/packages/Microsoft.DotNet.Xdt.Tools/) 

*`dotnet-xdt` is a [global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) that can only be installed on .NET Core 2.1 or later.*

**`dotnet-transform-xdt`** is an alternative version that can be installed
as a project-level tool, on all .NET Core versions. 

### <a name="msbuild"></a> Use with MSBuild/csproj tooling

**Note**: if you are using project.json tooling (CLI 1.0.0 preview 2 or earlier, or Visual Studio 2015),
please refer to the [project.json section below](#project-json).

Run `dotnet --version` in a command prompt and make sure you're using version **`2.0.0`** or later.

Create a new folder (`XdtSample`) and run `dotnet new web` inside it. Verify that the files
`XdtSample.csproj` and `web.config` file are present. Create a new file named `Web.Release.config`
inside that folder and set its content to:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration xmlns:xdt="http://schemas.microsoft.com/XML-Document-Transform">
  <system.webServer>
    <aspNetCore>
      <environmentVariables xdt:Transform="Insert">
        <environmentVariable name="DOTNET_CLI_TELEMETRY_OPTOUT" value="1" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

We will use this sample XDT file to add an environment variable that disables dotnet CLI telemetry when
your project is published using the `Release` configuration. See the [MSDN XDT reference](https://msdn.microsoft.com/en-us/library/dd465326.aspx)
for the complete transformation syntax.

Edit the `XdtSample.csproj` file and inside an `<ItemGroup>` element, add a reference to this XDT tool. 
Note that you cannot use the NuGet Package Manager UI in Visual Studio 2017 to CLI tool references; 
they must currently be added by editing the project file.

```xml
  <ItemGroup>
    <DotNetCliToolReference Include="Microsoft.DotNet.Xdt.Tools" Version="2.0.0" />
    ... other package references ...
  </ItemGroup>
```

Run `dotnet restore` and `dotnet build` in the `XdtSample` folder. If you now run `dotnet transform-xdt`
you will see the available  options, similar to:

```
.NET Core XML Document Transformation
Usage: dotnet transform-xdt [options]
Options:
  -?|-h|--help    Show help information
  --xml|-x        The path to the XML file to transform
  --transform|-t  The path to the XDT transform file to apply
  --output|-o     The path where the output (transformed) file will be written
  --verbose|-v    Print verbose messages
```

So far we added the XDT tool to the project, and now we will invoke it when the project is being published.
We want to call it before the built-in publish target that makes sure that the `Web.config` file has a reference
to the `aspNetCore` handler, because that target always runs when publishing web projects, and it also formats
the config file to be nicely indented.

Edit the `XdtSample.csproj` file and add this snippet at the end, right before the closing `</Project>` tag:

```xml
<Project ToolsVersion="15.0" Sdk="Microsoft.NET.Sdk.Web">
  ... everything else ...

  <Target Name="ApplyXdtConfigTransform" BeforeTargets="_TransformWebConfig">
    <PropertyGroup>
      <_SourceWebConfig>$(MSBuildThisFileDirectory)Web.config</_SourceWebConfig>
      <_XdtTransform>$(MSBuildThisFileDirectory)Web.$(Configuration).config</_XdtTransform>
      <_TargetWebConfig>$(PublishDir)Web.config</_TargetWebConfig>
    </PropertyGroup>
    <Exec
        Command="dotnet transform-xdt --xml &quot;$(_SourceWebConfig)&quot; --transform &quot;$(_XdtTransform)&quot; --output &quot;$(_TargetWebConfig)&quot;"
        Condition="Exists('$(_XdtTransform)')" />
  </Target>
</Project>
```

Here's a quick rundown of the values above:

  - `BeforeTargets="_TransformWebConfig"` schedules this target to run before the build-in target that adds
    the `aspNetCore` handler, as described earlier.
  - `_SourceWebConfig` defines the full path to the Web.config file in your **project** folder. This
    will be used as the source (input) for the transformation.
  - `_XdtTransform` defines the full path to the XDT transform file in your **project** folder to be applied.
    In this example, we use `Web.$(Configuration).config`, where $(Configuration) is a placeholder for the publish
    configuration, e.g. `Debug` or `Release`.
  - `_TargetWebConfig` defines the path where the transformed `Web.config` file will be written to, in the **publish** folder.
  - `Exec Command` invokes the XDT transform tool, passing the paths to the input file (`Web.config`), transform
    file (e.g. `Web.Release.config`) and target file (`<publish-folder>\Web.config`).
  - `Exec Condition` prevents the XDT transform tool from running if a transform file for a particular publish
    configuration does not exist (e.g. `Web.Debug.config`).

Now run `dotnet publish` in the `XdtSample` folder, and examine the `Web.config` in the publish output folder
(`bin\Debug\netcoreapp2.0\publish\Web.config`). It should look similar to this:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModule" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" arguments=".\XdtSample.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" forwardWindowsAuthToken="false" />
  </system.webServer>
</configuration>
```

Since we have not defined a `Web.Debug.config` file, no transformation occured.

Now let's publish again, but this time using the `Release` configuration. Run `dotnet publish -c Release`
in the `XdtSample` folder, and examine the `bin\Release\netcoreapp2.0\publish\Web.config` file.
It should look similar to this:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModule" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" arguments=".\XdtSample.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" forwardWindowsAuthToken="false">
      <environmentVariables>
        <environmentVariable name="DOTNET_CLI_TELEMETRY_OPTOUT" value="1" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

Note that under `<aspNetCore>`, the `<environmentVariables>` section was inserted, as configured in the
`Web.Release.config` file.

<h3><a name="project-json"></a>Use with <code>project.json</code> tooling</h3>

<details>
Add `Microsoft.DotNet.Xdt.Tools` to the `tools` sections of your `project.json` file:

```json
{
  ... other settings ...
  "tools": {
    "Microsoft.DotNet.Xdt.Tools": "1.0.0"
  }
}
```

##### Using [.NET Core 1.1](https://blogs.msdn.microsoft.com/dotnet/2016/11/16/announcing-net-core-1-1/) or [ASP.NET Core 1.1](https://blogs.msdn.microsoft.com/webdev/2016/11/16/announcing-asp-net-core-1-1/)?

In the sample above, replace `1.0.0` with `1.1.0`.

### How to Use (project.json tooling)

The typical use case is to transform `Web.config` (or similar XML-based files) at publish time.

As an example, let's apply a transformation based on the publish configuration (i.e. `Debug` vs.
`Release`). Add a `Web.Debug.config` file and a `Web.Release.config` file to your project, in the
same folder as `Web.config` file.

Call the tool from the `scripts/postpublish` section of your `project.json` to invoke it after publish:

```json
{
  "scripts": {
    "postpublish": [
        "dotnet transform-xdt --xml \"%publish:ProjectPath%\\Web.config\" --transform \"%publish:ProjectPath%\\Web.%publish:Configuration%.config\" --output \"%publish:OutputPath%\\Web.config\"",
        "dotnet publish-iis --publish-folder %publish:OutputPath% --framework %publish:FullTargetFramework%"
	]
  }
}
```

The following options are passed to `dotnet-transform-xdt`:
- `xml`: the input XML file to be transformed; in this example, the `Web.config` file in your **project** folder.
- `transform`: the XDT file to be applied; in this example, the `Web.Debug.config` file in your **project** folder.
- `output`: the XML file with the transformed output (input + XDT); in this example, the `Web.config` file
  in your **publish** folder (e.g. `bin\Debug\win7-x64\publish`).

With the above setup, calling `dotnet publish` from your project folder will apply the XDT transform
during the publishing process. The tool will print its output to the console, prefixed with
**`[XDT]`** markers.

You can pass an explicit configuration (e.g. `-c Debug` or `-c Release`) to `dotnet publish`
to specify the configuration (and thus applicable XDT file) to publish. A similar option is available in the Visual
Studio publish dialog.

Please note that varying the applied transform by configuration as shown above is just an example.
Any [dotnet publish variable](https://github.com/dotnet/cli/blob/f4ceb1f2136c5b0be16a7b551d28f5634a6c84bb/src/dotnet/commands/dotnet-publish/PublishCommand.cs#L108-L113)
can be used to drive the transformation process.

To get a list of all available options, run `dotnet transform-xdt` from the project folder:

```
.NET Core XML Document Transformation
Usage: dotnet transform-xdt [options]
Options:
  -?|-h|--help    Show help information
  --xml|-x        The path to the XML file to transform
  --transform|-t  The path to the XDT transform file to apply
  --output|-o     The path where the output (transformed) file will be written
  --verbose|-v    Print verbose messages
```
</details>
