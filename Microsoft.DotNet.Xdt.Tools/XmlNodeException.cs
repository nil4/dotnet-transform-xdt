using System;
using System.Xml;

namespace Microsoft.DotNet.Xdt.Tools
{
    public sealed class XmlNodeException : XmlTransformationException
    {
        private readonly XmlFileInfoDocument _document;
        private readonly IXmlLineInfo _lineInfo;

        public static Exception Wrap(Exception ex, XmlNode node)
        {
            if (ex is XmlNodeException) {
                // If this is already an XmlNodeException, then it probably
                // got its node closer to the error, making it more accurate
                return ex;
            }
            return new XmlNodeException(ex, node);
        }

        public XmlNodeException(Exception innerException, XmlNode node)
            : base(innerException.Message, innerException) {
            _lineInfo = node as IXmlLineInfo;
            _document = node.OwnerDocument as XmlFileInfoDocument;
        }

        public XmlNodeException(string message, XmlNode node)
            : base(message) {
            _lineInfo = node as IXmlLineInfo;
            _document = node.OwnerDocument as XmlFileInfoDocument;
        }

        public bool HasErrorInfo => _lineInfo != null;

        public string FileName => _document?.FileName;

        public int LineNumber => _lineInfo?.LineNumber ?? 0;

        public int LinePosition => _lineInfo?.LinePosition ?? 0;
    }
}
