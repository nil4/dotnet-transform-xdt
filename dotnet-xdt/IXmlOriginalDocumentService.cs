using System.Xml;

namespace Microsoft.DotNet.Xdt.Tools
{
    public interface IXmlOriginalDocumentService
    {
        XmlNodeList SelectNodes(string path, XmlNamespaceManager nsmgr);
    }
}
