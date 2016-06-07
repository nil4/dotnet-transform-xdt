dotnet-transform-xdt
===
`dotnet-transform-xdt` is a [dotnet CLI](https://github.com/dotnet/cli) tool for applying 
[XML Document Transformation](https://msdn.microsoft.com/en-us/library/dd465326.aspx) 
(typically, to ASP.NET configuration files at publish time, but not limited to this scenario). 

It is a port of <http://xdt.codeplex.com/> compatible with [.NET Core](http://dotnet.github.io/).

### How To Install

Add `Microsoft.DotNet.Xdt.Tools` to both the `dependencies` and `tools` sections of your `project.json` file:

```json
{
  "tools": {
    "Microsoft.DotNet.Xdt.Tools": {"version": "1.0.0-*"}
  }
}
```

### How To Use

The typical use case is to transform `Web.config` (or similar XML-based files) at publish time.

As an example, let's apply a transformation based on the publish configuration (i.e. `Debug` vs.
`Release`). Add a `Web.Debug.config` file and a `Web.Release.config` file to your project, in the 
same folder as `Web.config` file. 

See the [MSDN XDT reference](https://msdn.microsoft.com/en-us/library/dd465326.aspx)
for the transformation syntax. 

Call the tool from the `scripts/postpublish` section of your `project.json` to invoke it after publish:

```json
{
  "scripts": {
    "postpublish": [
        "dotnet transform-xdt --xml \"%publish:ProjectPath%\\web.config\" --transform \"%publish:ProjectPath%\\web.%publish:Configuration%.config\" --output \"%publish:OutputPath%\\Web.config\"",
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
as the last step of the publishing process. The tool will print its output to the console, prefixed with
**`[XDT]`** markers.

You can pass an explicit configuration (e.g. `-c Debug` or `-c Release`) to `dotnet publish` 
to specify the configuration (and thus XDT file) to publish. A similar option is available in the Visual
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

### Known issues

- `dotnet transform-xdt` must come before `dotnet publish-iis` 
- Logging and diagnostics is messy and should be cleaned up (like [dotnet-watch](https://github.com/aspnet/dotnet-watch))
- Unit tests have not been ported

Is the list above missing anything? Please [log an issue](https://github.com/nil4/dotnet-transform-xdt/issues).
