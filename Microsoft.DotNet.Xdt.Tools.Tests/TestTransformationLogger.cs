using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Xdt.Tools.Tests
{
    class TestTransformationLogger : IXmlTransformationLogger
    {
        readonly StringBuilder _log = new StringBuilder();
        int _indentLevel;

        string IndentString => new string(' ', 2 * _indentLevel);
        
        public string LogText => _log.ToString();

        public void LogMessage(string message, params object[] messageArgs) 
            => LogMessage(MessageType.Normal, message, messageArgs);

        public void LogMessage(MessageType type, string message, params object[] messageArgs) 
            => _log.AppendLine(string.Concat(IndentString, string.Format(message, messageArgs)));

        public void LogWarning(string message, params object[] messageArgs) 
            => LogWarning(message, messageArgs);

        public void LogWarning(string file, string message, params object[] messageArgs) 
            => LogWarning(file, 0, 0, message, messageArgs);

        // we will format like: transform.xml (30, 10) warning: Argument 'snap' did not match any attributes
        const string WarningFormat = "{0} ({1}, {2}) warning: {3}";
        public void LogWarning(string file, int lineNumber, int linePosition, string message, params object[] messageArgs) 
            => _log.AppendLine(string.Format(WarningFormat, Path.GetFileName(file), lineNumber, linePosition, string.Format(message,messageArgs)));

        public void LogError(string message, params object[] messageArgs) 
            => LogError("", message, messageArgs);

        public void LogError(string file, string message, params object[] messageArgs) 
            => LogError(file, 0, 0, message, messageArgs);

        //transform.xml(33, 10) error: Could not resolve 'ThrowException' as a type of Transform
        const string ErrorFormat = "{0} ({1}, {2}) error: {3}";
        public void LogError(string file, int lineNumber, int linePosition, string message, params object[] messageArgs) 
            => _log.AppendLine(string.Format(ErrorFormat, Path.GetFileName(file), lineNumber, linePosition, string.Format(message,messageArgs)));

        public void LogErrorFromException(Exception ex) {}

        public void LogErrorFromException(Exception ex, string file) {}

        public void LogErrorFromException(Exception ex, string file, int lineNumber, int linePosition) 
        {
            string message = ex.Message;
            LogError(file, lineNumber, linePosition, message);
        }

        public void StartSection(string message, params object[] messageArgs) 
            => StartSection(MessageType.Normal, message, messageArgs);

        public void StartSection(MessageType type, string message, params object[] messageArgs)
        {
            LogMessage(type, message, messageArgs);
            _indentLevel++;
        }

        public void EndSection(string message, params object[] messageArgs) 
            => EndSection(MessageType.Normal, message, messageArgs);

        public void EndSection(MessageType type, string message, params object[] messageArgs)
        {
            Debug.Assert(_indentLevel > 0);
            if (_indentLevel > 0) _indentLevel--;
            LogMessage(type, message, messageArgs);
        }
    }
}
