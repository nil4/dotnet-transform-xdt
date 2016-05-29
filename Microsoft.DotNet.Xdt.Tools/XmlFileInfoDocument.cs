using System;
using System.Text;
using System.Xml;
using System.IO;
using System.Diagnostics;

namespace Microsoft.DotNet.Xdt.Tools
{
    public class XmlFileInfoDocument : XmlDocument, IDisposable
    {
        private XmlReader _reader;

        private int _lineNumberOffset;
        private int _linePositionOffset;

        public override void Load(XmlReader reader) {
            if (_reader != null) {
                FileName = _reader.BaseURI;
            }

            base.Load(reader);
            //base.PreserveWhitespace = true;

            FirstLoad = false;
        }

        internal XmlNode CloneNodeFromOtherDocument(XmlNode element) {
            XmlReader oldReader = _reader;
            string oldFileName = FileName;

            XmlNode clone;
            try {
                var lineInfo = element as IXmlLineInfo;
                if (lineInfo != null) {
                    _reader = XmlReader.Create(new StringReader(element.OuterXml));

                    _lineNumberOffset = lineInfo.LineNumber - 1;
                    _linePositionOffset = lineInfo.LinePosition - 2;
                    FileName = element.OwnerDocument.BaseURI;

                    clone = ReadNode(_reader);
                }
                else {
                    FileName = null;
                    _reader = null;

                    clone = ReadNode(XmlReader.Create(new StringReader(element.OuterXml)));
                }
            }
            finally {
                _lineNumberOffset = 0;
                _linePositionOffset = 0;
                FileName = oldFileName;

                _reader = oldReader;
            }

            return clone;
        }

        private bool HasErrorInfo => _reader != null;

        internal string FileName { get; private set; }

        private int CurrentLineNumber => (_reader as IXmlLineInfo)?.LineNumber + _lineNumberOffset ?? 0;

        private int CurrentLinePosition => (_reader as IXmlLineInfo)?.LinePosition + _linePositionOffset ?? 0;

        private bool FirstLoad { get; set; } = true;

        private XmlAttributePreservationProvider PreservationProvider { get; set; }

        private Encoding TextEncoding {
            get {
                // Copied from base implementation of XmlDocument
                if (!HasChildNodes) return null;
                var declaration = FirstChild as XmlDeclaration;
                string value = declaration?.Encoding;
                return value?.Length > 0 
                    ? Encoding.GetEncoding(value) 
                    : null;
            }
        }

        public override void Save(TextWriter writer)
        {
            XmlWriter xmlWriter = null;
            try {
                if (PreserveWhitespace) {
                    XmlFormatter.Format(this);
                    xmlWriter = new XmlAttributePreservingWriter(writer);
                }
                else {
                    xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
                    {
                        Encoding = TextEncoding,
                        Indent = true,
                    });
                }
                WriteTo(xmlWriter);
            }
            finally
            {
                xmlWriter?.Flush();
                xmlWriter?.Dispose();
            }
        }

        public override void Save(Stream w)
        {
            XmlWriter xmlWriter = null;
            try {
                if (PreserveWhitespace) {
                    XmlFormatter.Format(this);
                    xmlWriter = new XmlAttributePreservingWriter(w, TextEncoding);
                }
                else {
                    xmlWriter = XmlWriter.Create(w, new XmlWriterSettings
                    {
                        Encoding = TextEncoding,
                        Indent = true,
                    });
                }
                WriteTo(xmlWriter);
            }
            finally
            {
                xmlWriter?.Flush();
            }
        }

        public override XmlElement CreateElement(string prefix, string localName, string namespaceURI) => HasErrorInfo 
            ? new XmlFileInfoElement(prefix, localName, namespaceURI, this) 
            : base.CreateElement(prefix, localName, namespaceURI);

        public override XmlAttribute CreateAttribute(string prefix, string localName, string namespaceURI) => HasErrorInfo 
            ? new XmlFileInfoAttribute(prefix, localName, namespaceURI, this) 
            : base.CreateAttribute(prefix, localName, namespaceURI);

        internal bool IsNewNode(XmlNode node) {
            // The transformation engine will only add elements. Anything
            // else that gets added must be contained by a new element.
            // So to determine what's new, we search up the tree for a new
            // element that contains this node.
            var element = FindContainingElement(node) as XmlFileInfoElement;
            return element != null && !element.IsOriginal;
        }

        private static XmlElement FindContainingElement(XmlNode node) {
            while (node != null && !(node is XmlElement)) {
                node = node.ParentNode;
            }
            return node as XmlElement;
        }

        private class XmlFileInfoElement : XmlElement, IXmlLineInfo, IXmlFormattableAttributes
        {
            private readonly XmlAttributePreservationDict _preservationDict;

            internal XmlFileInfoElement(string prefix, string localName, string namespaceUri, XmlFileInfoDocument document)
                : base(prefix, localName, namespaceUri, document) {
                LineNumber = document.CurrentLineNumber;
                LinePosition = document.CurrentLinePosition;
                IsOriginal = document.FirstLoad;

                if (document.PreservationProvider != null) {
                    _preservationDict = document.PreservationProvider.GetDictAtPosition(LineNumber, LinePosition - 1);
                }
                if (_preservationDict == null) {
                    _preservationDict = new XmlAttributePreservationDict();
                }
            }

            public override void WriteTo(XmlWriter w) {
                string prefix = Prefix;
                if (!string.IsNullOrEmpty(NamespaceURI)) {
                    prefix = w.LookupPrefix(NamespaceURI) ?? Prefix;
                }

                w.WriteStartElement(prefix, LocalName, NamespaceURI);

                if (HasAttributes) {
                    var preservingWriter = w as XmlAttributePreservingWriter;
                    if (preservingWriter == null || _preservationDict == null) {
                        WriteAttributesTo(w);
                    }
                    else {
                        WritePreservedAttributesTo(preservingWriter);
                    }
                }

                if (IsEmpty) {
                    w.WriteEndElement();
                }
                else {
                    WriteContentTo(w);
                    w.WriteFullEndElement();
                }
            }

            private void WriteAttributesTo(XmlWriter w) {
                XmlAttributeCollection attrs = Attributes;
                for (var i = 0; i < attrs.Count; i += 1) {
                    XmlAttribute attr = attrs[i];
                    attr.WriteTo(w);
                }
            }

            private void WritePreservedAttributesTo(XmlAttributePreservingWriter preservingWriter) 
                => _preservationDict.WritePreservedAttributes(preservingWriter, Attributes);

            public bool HasLineInfo() => true;

            public int LineNumber { get; }

            public int LinePosition { get; }

            public bool IsOriginal { get; }

            void IXmlFormattableAttributes.FormatAttributes(XmlFormatter formatter) 
                => _preservationDict.UpdatePreservationInfo(Attributes, formatter);

            string IXmlFormattableAttributes.AttributeIndent 
                => _preservationDict.GetAttributeNewLineString(null);
        }

        private class XmlFileInfoAttribute : XmlAttribute, IXmlLineInfo
        {
            internal XmlFileInfoAttribute(string prefix, string localName, string namespaceUri, XmlFileInfoDocument document)
                : base(prefix, localName, namespaceUri, document) {
                LineNumber = document.CurrentLineNumber;
                LinePosition = document.CurrentLinePosition;
            }

            public bool HasLineInfo() => true;

            public int LineNumber { get; }

            public int LinePosition { get; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }

            if (PreservationProvider != null)
            {
                PreservationProvider.Close();
                PreservationProvider = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~XmlFileInfoDocument()
        {
            Debug.Fail("call dispose please");
            Dispose(false);
        }
    }
}
