using System;
using System.Xml;
using System.Diagnostics;
using System.Globalization;

namespace DotNet.Xdt
{
    public class XmlTransformationLogger
    {
        readonly IXmlTransformationLogger _externalLogger;
        XmlNode? _currentReferenceNode;

        internal XmlTransformationLogger(IXmlTransformationLogger logger) 
            => _externalLogger = logger;

        internal void LogErrorFromException(Exception ex)
        {
            HasLoggedErrors = true;

            if (_externalLogger is not null)
            {
                if (ex is XmlNodeException nodeException && nodeException.HasErrorInfo)
                {
                    _externalLogger.LogErrorFromException(
                        nodeException,
                        ConvertUriToFileName(nodeException.FileName),
                        nodeException.LineNumber,
                        nodeException.LinePosition);
                }
                else
                    _externalLogger.LogErrorFromException(ex);
            }
            else
                throw ex;
        }

        internal bool HasLoggedErrors { get; set; }

        internal XmlNode? CurrentReferenceNode
        {
            get => _currentReferenceNode;
            set
            {
                // I don't feel like implementing a stack for this for no
                // reason. Only one thing should try to set this property
                // at a time, and that thing should clear it when done.
                Debug.Assert(_currentReferenceNode is null || value is null, "CurrentReferenceNode is being overwritten");
                _currentReferenceNode = value;
            }
        }

        public bool SupressWarnings { get; set; }

        public void LogMessage(string message, params object[] messageArgs) 
            => _externalLogger?.LogMessage(message, messageArgs);

        public void LogMessage(MessageType type, string message, params object[] messageArgs) 
            => _externalLogger?.LogMessage(type, message, messageArgs);

        public void LogWarning(string message, params object[] messageArgs)
        {
            // SupressWarnings downgrade the Warning to LogMessage
            if (SupressWarnings)
                LogMessage(message, messageArgs);
            else
            {
                if (CurrentReferenceNode is not null)
                    LogWarning(CurrentReferenceNode, message, messageArgs);
                else
                    _externalLogger?.LogWarning(message, messageArgs);
            }
        }

        public void LogWarning(XmlNode referenceNode, string message, params object[] messageArgs)
        {
            // SupressWarnings downgrade the Warning to LogMessage
            if (SupressWarnings)
                LogMessage(message, messageArgs);
            else
            {
                if (_externalLogger is not null)
                {
                    string? fileName = ConvertUriToFileName(referenceNode.OwnerDocument);

                    if (referenceNode is IXmlLineInfo lineInfo)
                    {
                        _externalLogger.LogWarning(
                            fileName,
                            lineInfo.LineNumber,
                            lineInfo.LinePosition,
                            message,
                            messageArgs);
                    }
                    else
                    {
                        _externalLogger.LogWarning(
                            fileName,
                            message,
                            messageArgs);
                    }
                }
            }
        }

        public void LogError(string message, params object[] messageArgs)
        {
            HasLoggedErrors = true;

            if (CurrentReferenceNode is not null)
                LogError(CurrentReferenceNode, message, messageArgs);
            else if (_externalLogger is not null)
                _externalLogger.LogError(message, messageArgs);
            else
                throw new XmlTransformationException(string.Format(CultureInfo.CurrentCulture, message, messageArgs));
        }

        public void LogError(XmlNode referenceNode, string message, params object[] messageArgs)
        {
            HasLoggedErrors = true;

            if (_externalLogger is not null)
            {
                string? fileName = ConvertUriToFileName(referenceNode.OwnerDocument);

                if (referenceNode is IXmlLineInfo lineInfo)
                {
                    _externalLogger.LogError(
                        fileName,
                        lineInfo.LineNumber,
                        lineInfo.LinePosition,
                        message,
                        messageArgs);
                }
                else
                {
                    _externalLogger.LogError(
                        fileName,
                        message,
                        messageArgs);
                }
            }
            else
                throw new XmlNodeException(string.Format(CultureInfo.CurrentCulture, message, messageArgs), referenceNode);
        }

        public void StartSection(string message, params object[] messageArgs) 
            => _externalLogger?.StartSection(message, messageArgs);

        public void StartSection(MessageType type, string message, params object[] messageArgs) 
            => _externalLogger?.StartSection(type, message, messageArgs);

        public void EndSection(string message, params object[] messageArgs) 
            => _externalLogger?.EndSection(message, messageArgs);

        public void EndSection(MessageType type, string message, params object[] messageArgs) 
            => _externalLogger?.EndSection(type, message, messageArgs);

        static string? ConvertUriToFileName(XmlDocument? xmlDocument)
        {
            string? uri = xmlDocument is XmlFileInfoDocument errorInfoDocument ? errorInfoDocument.FileName : xmlDocument!.BaseURI;
            return ConvertUriToFileName(uri);
        }

        static string? ConvertUriToFileName(string? fileName)
        {
            try
            {
                var uri = new Uri(fileName);
                if (uri.IsFile && string.IsNullOrEmpty(uri.Host))
                    fileName = uri.LocalPath;
            }
            catch (UriFormatException)
            {
                // Bad URI format, so just return the original filename
            }

            return fileName;
        }
    }
}
