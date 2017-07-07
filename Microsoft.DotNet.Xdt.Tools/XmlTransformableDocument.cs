using System.Xml;

namespace Microsoft.DotNet.Xdt.Tools
{
    public class XmlTransformableDocument : XmlFileInfoDocument, IXmlOriginalDocumentService
    {
        private XmlDocument _xmlOriginal;

        public bool IsChanged => _xmlOriginal != null && !IsXmlEqual(_xmlOriginal, this);

        internal void OnBeforeChange()
        {
            if (_xmlOriginal == null)
            {
                CloneOriginalDocument();
            }
        }

        internal void OnAfterChange()
        {
        }

        private void CloneOriginalDocument()
        {
            _xmlOriginal = (XmlDocument)CloneNode(true);
        }

        private static bool IsXmlEqual(XmlDocument xmlOriginal, XmlDocument xmlTransformed)
        {
            // FUTURE: Write a comparison algorithm to see if xmlLeft and
            // xmlRight are different in any significant way. Until then,
            // assume there's a difference.
            return false;
        }

        XmlNodeList IXmlOriginalDocumentService.SelectNodes(string xpath, XmlNamespaceManager nsmgr) => _xmlOriginal?.SelectNodes(xpath, nsmgr);
    }
}
