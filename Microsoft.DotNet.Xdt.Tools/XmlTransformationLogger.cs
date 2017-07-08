using System;
using System.Xml;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.DotNet.Xdt.Tools
{
    public class XmlTransformationLogger
    {
        private readonly IXmlTransformationLogger _externalLogger;
        private XmlNode _currentReferenceNode;

        internal XmlTransformationLogger(IXmlTransformationLogger logger)
        {
            _externalLogger = logger;
        }

        internal void LogErrorFromException(Exception ex)
        {
            HasLoggedErrors = true;

            if (_externalLogger != null)
            {
                var nodeException = ex as XmlNodeException;
                if (nodeException != null && nodeException.HasErrorInfo)
                {
                    _externalLogger.LogErrorFromException(
                        nodeException,
                        ConvertUriToFileName(nodeException.FileName),
                        nodeException.LineNumber,
                        nodeException.LinePosition);
                }
                else
                {
                    _externalLogger.LogErrorFromException(ex);
                }
            }
            else
            {
                throw ex;
            }
        }

        internal bool HasLoggedErrors { get; set; }

        internal XmlNode CurrentReferenceNode
        {
            get
            {
                return _currentReferenceNode;
            }
            set
            {
                // I don't feel like implementing a stack for this for no
                // reason. Only one thing should try to set this property
                // at a time, and that thing should clear it when done.
                Debug.Assert(_currentReferenceNode == null || value == null, "CurrentReferenceNode is being overwritten");

                _currentReferenceNode = value;
            }
        }

        public bool SupressWarnings { get; set; }

        public void LogMessage(string message, params object[] messageArgs) => _externalLogger?.LogMessage(message, messageArgs);

        public void LogMessage(MessageType type, string message, params object[] messageArgs) => _externalLogger?.LogMessage(type, message, messageArgs);

        public void LogWarning(string message, params object[] messageArgs)
        {
            if (SupressWarnings)
            {
                // SupressWarnings downgrade the Warning to LogMessage
                LogMessage(message, messageArgs);
            }
            else
            {
                if (CurrentReferenceNode != null)
                {
                    LogWarning(CurrentReferenceNode, message, messageArgs);
                }
                else
                {
                    _externalLogger?.LogWarning(message, messageArgs);
                }
            }
        }

        public void LogWarning(XmlNode referenceNode, string message, params object[] messageArgs)
        {
            if (SupressWarnings)
            {
                // SupressWarnings downgrade the Warning to LogMessage
                LogMessage(message, messageArgs);
            }
            else
            {
                if (_externalLogger != null)
                {
                    string fileName = ConvertUriToFileName(referenceNode.OwnerDocument);
                    var lineInfo = referenceNode as IXmlLineInfo;

                    if (lineInfo != null)
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

            if (CurrentReferenceNode != null)
            {
                LogError(CurrentReferenceNode, message, messageArgs);
            }
            else if (_externalLogger != null)
            {
                _externalLogger.LogError(message, messageArgs);
            }
            else
            {
                throw new XmlTransformationException(string.Format(CultureInfo.CurrentCulture, message, messageArgs));
            }
        }

        public void LogError(XmlNode referenceNode, string message, params object[] messageArgs)
        {
            HasLoggedErrors = true;

            if (_externalLogger != null)
            {
                string fileName = ConvertUriToFileName(referenceNode.OwnerDocument);
                var lineInfo = referenceNode as IXmlLineInfo;

                if (lineInfo != null)
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
            {
                throw new XmlNodeException(string.Format(CultureInfo.CurrentCulture, message, messageArgs), referenceNode);
            }
        }

        public void StartSection(string message, params object[] messageArgs) => _externalLogger?.StartSection(message, messageArgs);

        public void StartSection(MessageType type, string message, params object[] messageArgs) => _externalLogger?.StartSection(type, message, messageArgs);

        public void EndSection(string message, params object[] messageArgs) => _externalLogger?.EndSection(message, messageArgs);

        public void EndSection(MessageType type, string message, params object[] messageArgs) => _externalLogger?.EndSection(type, message, messageArgs);

        private string ConvertUriToFileName(XmlDocument xmlDocument)
        {
            var errorInfoDocument = xmlDocument as XmlFileInfoDocument;
            string uri = errorInfoDocument != null ? errorInfoDocument.FileName : xmlDocument.BaseURI;

            return ConvertUriToFileName(uri);
        }

        private static string ConvertUriToFileName(string fileName)
        {
            try
            {
                var uri = new Uri(fileName);
                if (uri.IsFile && string.IsNullOrEmpty(uri.Host))
                {
                    fileName = uri.LocalPath;
                }
            }
            catch (UriFormatException)
            {
                // Bad URI format, so just return the original filename
            }

            return fileName;
        }
    }
}
