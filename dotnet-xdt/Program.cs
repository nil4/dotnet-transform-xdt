using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

#if NETCOREAPP
namespace DotNet.Xdt
{
    static class Program
    {
        const string ToolName = "dotnet-xdt";
        const string Prefix = "[" + ToolName + "] ";

        const int Success = 0;
        const int ErrorUsage = 1;
        const int ErrorFailed = 2;

        public static int Main(string[] args)
        {
            string inputFilePath = null, outputFilePath = null, transformFilePath = null;
            bool verbose = false, quiet = false, printUsage = false;

            if (!ParseArguments(args, ref inputFilePath, ref outputFilePath, ref transformFilePath, ref verbose, ref quiet, ref printUsage))
            {
                if (printUsage)
                {
                    PrintUsage(Console.Out);
                    return ErrorUsage;
                }

                PrintUsage(Console.Error);
                return ErrorUsage;
            }

            if (!File.Exists(inputFilePath))
            {
                LogError($"Input file not found: {inputFilePath}");
                return ErrorUsage;
            }

            if (!File.Exists(transformFilePath))
            {
                LogError($"Transform file not found: {transformFilePath}");
                return ErrorUsage;
            }

            if (!quiet) Log($"Transforming '{inputFilePath}' using '{transformFilePath}' into '{outputFilePath}'");

            try
            {
                var sourceXml = new XmlTransformableDocument { PreserveWhitespace = true };

                using (var sourceStream = File.OpenRead(inputFilePath))
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
                LogError($"Failed: {ex}");
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
            writer.WriteLine("  --xml|-x         Input XML file to transform");
            writer.WriteLine("  --transform|-t   XDT transform file to apply");
            writer.WriteLine("  --output|-o      Path where the output file will be written");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine("  --help|-h|-?     Print usage information");
            writer.WriteLine("  --quiet|-q       Print only error messages");
            writer.WriteLine("  --verbose|-v     Print verbose messages while transforming");
            writer.WriteLine();
            writer.WriteLine($"Example: {ToolName} --xml original.xml --transform delta.xml --output final.xml --verbose");
        }

        static bool ParseArguments(IReadOnlyList<string> args, ref string inputFilePath, ref string outputFilePath, 
            ref string transformFilePath, ref bool verbose, ref bool quiet, ref bool showHelp)
        {
            for (var i = 0; i < args.Count; i++)
            {
                switch (args[i])
                {
                case "-x":
                case "--xml":
                    if (!TryRead(i + 1, ref inputFilePath)) return false;
                    ++i;
                    continue;

                case "-o":
                case "--output":
                    if (!TryRead(i + 1, ref outputFilePath)) return false;
                    ++i;
                    continue;

                case "-t":
                case "--transform":
                    if (!TryRead(i + 1, ref transformFilePath)) return false;
                    ++i;
                    continue;

                case "-v":
                case "--verbose":
                    verbose = true;
                    break;

                case "-q":
                case "--quiet":
                    quiet = true;
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

            return !string.IsNullOrWhiteSpace(inputFilePath)
                && !string.IsNullOrWhiteSpace(outputFilePath)
                && !string.IsNullOrWhiteSpace(transformFilePath);

            bool TryRead(int index, ref string value)
            {
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
#endif