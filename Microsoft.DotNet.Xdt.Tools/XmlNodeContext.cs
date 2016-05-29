using System.Xml;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal class XmlNodeContext
    {
        public XmlNodeContext(XmlNode node) {
            Node = node;
        }

        public XmlNode Node { get; }

        public bool HasLineInfo => Node is IXmlLineInfo;

        protected int LineNumber => (Node as IXmlLineInfo)?.LineNumber ?? 0;

        protected int LinePosition => (Node as IXmlLineInfo)?.LinePosition ?? 0;
    }
}
