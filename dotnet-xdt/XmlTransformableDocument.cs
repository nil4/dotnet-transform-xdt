using System.Xml;

namespace DotNet.Xdt
{
    public class XmlTransformableDocument : XmlFileInfoDocument, IXmlOriginalDocumentService
    {
        XmlDocument? _xmlOriginal;

        public bool IsChanged => _xmlOriginal != null && !IsXmlEqual(_xmlOriginal, this);

        internal void OnBeforeChange()
        {
            if (_xmlOriginal == null)
                CloneOriginalDocument();
        }

        internal void OnAfterChange()
        { }

        void CloneOriginalDocument() => _xmlOriginal = (XmlDocument) Clone();

        // FUTURE: Write a comparison algorithm to see if xmlLeft and
        // xmlRight are different in any significant way. Until then,
        // assume there's a difference.
        static bool IsXmlEqual(XmlDocument xmlOriginal, XmlDocument xmlTransformed) => false;

        XmlNodeList? IXmlOriginalDocumentService.SelectNodes(string xpath, XmlNamespaceManager nsmgr) 
            => _xmlOriginal?.SelectNodes(xpath, nsmgr);
    }
}
