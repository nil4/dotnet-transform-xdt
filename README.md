# dotnet-transform-xdt [![Build status](https://ci.appveyor.com/api/projects/status/559na9y3iswe9hbh/branch/master?svg=true)](https://ci.appveyor.com/project/nil4/dotnet-transform-xdt/branch/master)

`dotnet-transform-xdt` is a [dotnet CLI](https://github.com/dotnet/cli) tool for applying
[XML Document Transformation](https://msdn.microsoft.com/en-us/library/dd465326.aspx)
(typically, to ASP.NET configuration files at publish time, but not limited to this scenario).

It is a port of <http://xdt.codeplex.com/> compatible with [.NET Core](http://dotnet.github.io/).

### Sample projects

[A separate repository](https://github.com/nil4/xdt-samples/) includes a few sample projects using this tool for Web.config
transformations at publish time. Clone the repository to test the scenarios out, or
[review the commit history](https://github.com/nil4/xdt-samples/commits/master) for
the individual steps.

### <a name="msbuild"></a> How to use with MSBuild/csproj tooling

**Note**: if you are using project.json tooling (CLI preview 2 or earlier, or Visual Studio 2015),
please refer to the [project.json section below](#project-json).

Run `dotnet --version` in a command prompt and make sure you're using version **`2.0.0-preview2`** or later.

Create a new folder (`XdtSample`) and run `dotnet new -t web` inside it. Verify that the files
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
    <DotNetCliToolReference Include="Microsoft.DotNet.Xdt.Tools" Version="2.0.0-preview1" />
    ... other package references ...
  <ItemGroup>
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


### <a name="project-json"></a> How to Install (project.json tooling)

**Note**: if you are using MSBuild/csproj tooling (CLI preview 4 or later, or Visual Studio 2017),
please refer to the [MSBuild/csproj section above](#msbuild).

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

See the [MSDN XDT reference](https://msdn.microsoft.com/en-us/library/dd465326.aspx)
for the complete transformation syntax.

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
