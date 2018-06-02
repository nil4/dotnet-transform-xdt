using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace DotNet.Xdt
{
    static class Program
    {
        const string ToolName = "dotnet-xdt";
        const string Prefix = "[" + ToolName + "] ";

        const int Success = 0;
        const int ErrorUsage = 1;
        const int ErrorFailed = 2;

        static int Main(string[] args)
        {
            string sourceFilePath = null, outputFilePath = null, transformFilePath = null;
            bool verbose = false, printUsage = false;

            if (!ParseArguments(args, ref sourceFilePath, ref outputFilePath, ref transformFilePath, ref verbose, ref printUsage))
            {
                if (printUsage)
                {
                    PrintUsage(Console.Out);
                    return ErrorUsage;
                }

                PrintUsage(Console.Error);
                return ErrorUsage;
            }

            if (!File.Exists(sourceFilePath))
            {
                LogError($"Source file not found: {sourceFilePath}");
                return ErrorUsage;
            }

            if (!File.Exists(transformFilePath))
            {
                LogError($"Transform file not found: {transformFilePath}");
                return ErrorUsage;
            }

            Log($"Transforming '{sourceFilePath}' using '{transformFilePath}' into '{outputFilePath}'");

            try
            {
                var sourceXml = new XmlTransformableDocument { PreserveWhitespace = true };

                using (var sourceStream = File.OpenRead(sourceFilePath))
                using (var transformStream = File.OpenRead(transformFilePath))
                using (var transformation = new XmlTransformation(transformStream, new ConsoleTransformationLogger(verbose)))
                {
                    sourceXml.Load(sourceStream);
                    transformation.Apply(sourceXml);
                }

                using (FileStream outputStream = File.Create(outputFilePath))
                using (var outputWriter = XmlWriter.Create(outputStream, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
                {
                    sourceXml.WriteTo(outputWriter);
                }

                return Success;
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error: {ex}");
                return ErrorFailed;
            }
        }

        static void Log(FormattableString message)
        {
            Console.Write(Prefix);
            Console.WriteLine(message.ToString(CultureInfo.InvariantCulture));
        }

        static void LogError(FormattableString message)
        {
            Console.Error.Write(Prefix);
            Console.Error.WriteLine(message.ToString(CultureInfo.InvariantCulture));
        }

        static void PrintUsage(TextWriter writer)
        {
            writer.WriteLine(".NET XML Document Transform");
            writer.WriteLine();
            writer.WriteLine($"Usage: {ToolName} <arguments> [options]");
            writer.WriteLine();
            writer.WriteLine("Required arguments:");
            writer.WriteLine("  --source|-s      Source XML file to transform");
            writer.WriteLine("  --transform|-t   XDT transform file to apply");
            writer.WriteLine("  --output|-o      Path where the output file will be written");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine("  --help|-h|-?     Print usage information");
            writer.WriteLine("  --quiet|-q       Print only error messages");
            writer.WriteLine("  --verbose|-v     Print verbose messages while transforming");
            writer.WriteLine();
            writer.WriteLine($"Example: {ToolName} --source original.xml --transform delta.xml --output final.xml --verbose");
        }

        static bool ParseArguments(IReadOnlyList<string> args, ref string sourceFilePath, ref string outputFilePath, 
            ref string transformFilePath, ref bool verbose, ref bool showHelp)
        {
            for (var i = 0; i < args.Count; i++)
            {
                switch (args[i])
                {
                case "-s":
                case "--source":
                case "-x":      // back-compat alias
                case "--xml":   // back-compat alias
                    if (!TryRead(ref i, ref sourceFilePath)) return false;
                    break;

                case "-o":
                case "--output":
                    if (!TryRead(ref i, ref outputFilePath)) return false;
                    break;

                case "-t":
                case "--transform":
                    if (!TryRead(ref i, ref transformFilePath)) return false;
                    break;

                case "-v":
                case "--verbose":
                    verbose = true;
                    break;

                case "-?":
                case "-h":
                case "--help":
                    showHelp = true;
                    return false;

                default:
                    LogError($"Invalid argument: '{args[i]}'");
                    return false;
                }
            }

            return !string.IsNullOrWhiteSpace(sourceFilePath)
                && !string.IsNullOrWhiteSpace(outputFilePath)
                && !string.IsNullOrWhiteSpace(transformFilePath);

            bool TryRead(ref int index, ref string value)
            {
                ++index;
                if (index >= args.Count || value != null) return false;
                value = args[index];
                return true;
            }
        }

        sealed class ConsoleTransformationLogger : IXmlTransformationLogger
        {
            readonly bool _verbose;

            internal ConsoleTransformationLogger(bool verbose) 
                => _verbose = verbose;

            bool ShouldPrint(MessageType type) 
                => type == MessageType.Normal || _verbose;

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
