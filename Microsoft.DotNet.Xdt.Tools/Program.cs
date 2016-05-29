using System;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.DotNet.Xdt.Tools
{
    public class Program
    {
        private const string Prefix = "[XDT] ";

        public static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "dotnet transform xdt",
                FullName = ".NET Core XML Document Transformation",
                Description = "XML Document Transformation for .NET Core applications"
            };
            app.HelpOption("-h|--help");

            var inputFilePath = app.Option("--input|-i", "The path to the input XML file to transform", CommandOptionType.SingleValue);
            var transformFilePath = app.Option("--transform|-t", "The path to the XDT transform file to apply", CommandOptionType.SingleValue);
            var outputFilePath = app.Option("--output|-o", "The path where the transformed file will be written", CommandOptionType.SingleValue);
            var verboseOption = app.Option("--verbose|-v", "Print verbose messages", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                string inputPath = inputFilePath.Value();
                string transformPath = transformFilePath.Value();
                string outputPath = outputFilePath.Value();

                if (inputPath == null || transformPath == null || outputPath == null)
                {
                    app.ShowHelp();
                    return 2;
                }

                if (!File.Exists(inputPath))
                {
                    Console.Error.WriteLine($"{Prefix}Input file not found: {inputPath}");
                    return 3;
                }
                if (!File.Exists(transformPath))
                {
                    Console.Error.WriteLine($"{Prefix}Transform file not found: {transformPath}");
                    return 4;
                }

                Console.WriteLine($"{Prefix}Transforming '{inputPath}' using '{transformPath}' into '{outputPath}'");

                using (var sourceStream = File.OpenRead(inputPath))
                using (var transformStream = File.OpenRead(transformPath))
                using (var transformation = new XmlTransformation(transformStream, new ConsoleTransformationLogger(verboseOption.HasValue())))
                {
                    var sourceXml = new XmlTransformableDocument { PreserveWhitespace = true };
                    sourceXml.Load(sourceStream);
                    transformation.Apply(sourceXml);

                    using (var outputStream = File.Create(outputPath))
                    using (var outputWriter = XmlWriter.Create(outputStream, new XmlWriterSettings
                    {
                        Indent = true,
                        Encoding = new UTF8Encoding(false, true),
                    }))
                    {
                        sourceXml.WriteTo(outputWriter);
                    }
                }
                return 0;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(Prefix + "Failed: " + ex.Message);
                return 1;
            }
        }

        private sealed class ConsoleTransformationLogger : IXmlTransformationLogger
        {
            private readonly bool _verbose;

            internal ConsoleTransformationLogger(bool verbose)
            {
                _verbose = verbose;
            }

            private bool ShouldPrint(MessageType type) => type == MessageType.Normal || _verbose;

            public void LogMessage(string message, params object[] messageArgs) 
                => Console.WriteLine(Prefix + message, messageArgs);

            public void LogMessage(MessageType type, string message, params object[] messageArgs)
            {
                if (ShouldPrint(type)) Console.WriteLine($"{Prefix}{type}: {message}", messageArgs);
            }

            public void LogWarning(string message, params object[] messageArgs) 
                => Console.WriteLine($"{Prefix}WARN: {message}", messageArgs);

            public void LogWarning(string file, string message, params object[] messageArgs) 
                => Console.WriteLine($"{Prefix}WARN '{file}': {message}", messageArgs);

            public void LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs) 
                => Console.WriteLine($"{Prefix}WARN '{file}':{lineNumber}:{linePosition}: {message}", messageArgs);

            public void LogError(string message, params object[] messageArgs)
                => Console.Error.WriteLine($"{Prefix}ERROR: {message}", messageArgs);

            public void LogError(string file, string message, params object[] messageArgs)
                => Console.Error.WriteLine($"{Prefix}ERROR '{file}': {message}", messageArgs);

            public void LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs)
                => Console.Error.WriteLine($"{Prefix}ERROR '{file}':{lineNumber}:{linePosition}: {message}", messageArgs);

            public void LogErrorFromException(Exception ex)
                => Console.Error.WriteLine($"{Prefix}ERROR: {ex}");

            public void LogErrorFromException(Exception ex, string file)
                => Console.Error.WriteLine($"{Prefix}ERROR '{file}': {ex}");

            public void LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition)
                => Console.Error.WriteLine($"{Prefix}ERROR '{file}':{lineNumber}:{linePosition}: {ex}");

            public void StartSection(string message, params object[] messageArgs)
                => Console.WriteLine($"{Prefix}Start {message}", messageArgs);

            public void StartSection(MessageType type, string message, params object[] messageArgs)
            {
                if (ShouldPrint(type)) Console.WriteLine($"{Prefix}{type}: Start {message}", messageArgs);
            }

            public void EndSection(string message, params object[] messageArgs)
                => Console.WriteLine($"{Prefix}End {message}", messageArgs);

            public void EndSection(MessageType type, string message, params object[] messageArgs)
            {
                if (ShouldPrint(type)) Console.WriteLine($"{Prefix}{type}: End {message}", messageArgs);
            }
        }
    }
}