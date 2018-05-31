using System.Xml;

namespace DotNet.Xdt
{
    interface IXmlOriginalDocumentService
    {
        XmlNodeList SelectNodes(string path, XmlNamespaceManager nsmgr);
    }
}
