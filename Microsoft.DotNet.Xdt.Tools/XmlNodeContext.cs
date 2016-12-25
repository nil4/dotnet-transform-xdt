using System.Xml;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal class XmlNodeContext
    {
        private readonly XmlNode _node;

        public XmlNodeContext(XmlNode node)
        {
            _node = node;
        }

        public XmlNode Node => _node;

        public bool HasLineInfo => _node is IXmlLineInfo;

        public int LineNumber => (_node as IXmlLineInfo)?.LineNumber ?? 0;

        public int LinePosition => (_node as IXmlLineInfo)?.LinePosition ?? 0;
    }
}
