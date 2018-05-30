using System.Xml;

namespace DotNet.Xdt
{
    class XmlNodeContext
    {
        public XmlNodeContext(XmlNode node) => Node = node;

        public XmlNode Node { get; }

        public bool HasLineInfo => Node is IXmlLineInfo;

        public int LineNumber => (Node as IXmlLineInfo)?.LineNumber ?? 0;

        public int LinePosition => (Node as IXmlLineInfo)?.LinePosition ?? 0;
    }
}
