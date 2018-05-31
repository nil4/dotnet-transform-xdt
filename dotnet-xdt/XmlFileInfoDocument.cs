using System;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.IO;

namespace DotNet.Xdt
{
    public class XmlFileInfoDocument : XmlDocument, IDisposable
    {
        Encoding _textEncoding;
        XmlTextReader _reader;

        int _lineNumberOffset;
        int _linePositionOffset;

        public override void Load(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("File name must not be empty", nameof(filename));

            LoadFromFileName(filename);

            FirstLoad = false;
        }

        public override void Load(XmlReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            _reader = reader as XmlTextReader;
            if (_reader != null)
                FileName = _reader.BaseURI;

            base.Load(reader);

            if (_reader != null)
                _textEncoding = _reader.Encoding;

            FirstLoad = false;
        }

        void LoadFromFileName(string filename)
        {
            FileName = filename;

            StreamReader reader = null;
            try
            {
                if (PreserveWhitespace)
                    PreservationProvider = new XmlAttributePreservationProvider(filename);

                reader = new StreamReader(filename, true);
                LoadFromTextReader(reader);
            }
            finally
            {
                if (PreservationProvider != null)
                {
                    PreservationProvider.Close();
                    PreservationProvider = null;
                }
                reader?.Close();
            }
        }

        void LoadFromTextReader(TextReader textReader)
        {
            if (textReader is StreamReader streamReader)
            {
                if (streamReader.BaseStream is FileStream fileStream)
                    FileName = fileStream.Name;

                _textEncoding = GetEncodingFromStream(streamReader.BaseStream);
            }

            _reader = new XmlTextReader(FileName, textReader);

            base.Load(_reader);

            if (_textEncoding == null)
                _textEncoding = _reader.Encoding;
        }

        static Encoding GetEncodingFromStream(Stream stream)
        {
            Encoding encoding = null;
            if (stream.CanSeek)
            {
                var buffer = new byte[3];
                stream.Read(buffer, 0, buffer.Length);

                if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    encoding = Encoding.UTF8;
                else if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    encoding = Encoding.BigEndianUnicode;
                else if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    encoding = Encoding.Unicode;
                else if (buffer[0] == 0x2B && buffer[1] == 0x2F && buffer[2] == 0x76)
                    encoding = Encoding.UTF7;

                // Reset the stream
                stream.Seek(0, SeekOrigin.Begin);
            }

            return encoding;
        }

        internal XmlNode CloneNodeFromOtherDocument(XmlNode element)
        {
            XmlTextReader oldReader = _reader;
            string oldFileName = FileName;

            XmlNode clone;
            try
            {
                if (element is IXmlLineInfo lineInfo)
                {
                    _reader = new XmlTextReader(new StringReader(element.OuterXml));

                    _lineNumberOffset = lineInfo.LineNumber - 1;
                    _linePositionOffset = lineInfo.LinePosition - 2;
                    FileName = element.OwnerDocument.BaseURI;

                    clone = ReadNode(_reader);
                }
                else
                {
                    FileName = null;
                    _reader = null;

                    clone = ReadNode(new XmlTextReader(new StringReader(element.OuterXml)));
                }
            }
            finally
            {
                _lineNumberOffset = 0;
                _linePositionOffset = 0;
                FileName = oldFileName;

                _reader = oldReader;
            }

            return clone;
        }

        internal bool HasErrorInfo => _reader != null;

        internal string FileName { get; private set; }

        int CurrentLineNumber => _reader?.LineNumber + _lineNumberOffset ?? 0;

        int CurrentLinePosition => _reader?.LinePosition + _linePositionOffset ?? 0;

        bool FirstLoad { get; set; } = true;

        XmlAttributePreservationProvider PreservationProvider { get; set; }

        Encoding TextEncoding
        {
            get
            {
                if (_textEncoding != null)
                    return _textEncoding;
                
                // Copied from base implementation of XmlDocument
                if (HasChildNodes)
                {
                    var declaration = FirstChild as XmlDeclaration;
                    string value = declaration?.Encoding;
                    if (value?.Length > 0)
                        return Encoding.GetEncoding(value);
                }
                return null;
            }
        }

        public override void Save(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("File name cannot be empty", nameof(filename));

            XmlWriter xmlWriter = null;
            try
            {
                if (PreserveWhitespace)
                {
                    XmlFormatter.Format(this);
                    xmlWriter = new XmlAttributePreservingWriter(filename, TextEncoding);
                }
                else
                {
                    var textWriter = new XmlTextWriter(filename, TextEncoding) { Formatting = Formatting.Indented };
                    xmlWriter = textWriter;
                }
                
                WriteTo(xmlWriter);
            }
            finally
            {
                if (xmlWriter != null)
                {
                    xmlWriter.Flush();
                    xmlWriter.Close();
                }
            }
        }

        public override void Save(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            XmlWriter xmlWriter = null;
            try
            {
                if (PreserveWhitespace)
                {
                    XmlFormatter.Format(this);
                    xmlWriter = new XmlAttributePreservingWriter(stream, TextEncoding);
                }
                else
                {
                    XmlTextWriter textWriter = new XmlTextWriter(stream, TextEncoding) {Formatting = Formatting.Indented};
                    xmlWriter = textWriter;
                }
                WriteTo(xmlWriter);
            }
            finally
            {
                xmlWriter?.Flush();
            }
        }

        public override XmlElement CreateElement(string prefix, string localName, string namespaceUri) 
            => HasErrorInfo ? new XmlFileInfoElement(prefix, localName, namespaceUri, this) : base.CreateElement(prefix, localName, namespaceUri);

        public override XmlAttribute CreateAttribute(string prefix, string localName, string namespaceUri) 
            => HasErrorInfo ? new XmlFileInfoAttribute(prefix, localName, namespaceUri, this) : base.CreateAttribute(prefix, localName, namespaceUri);

        internal bool IsNewNode(XmlNode node)
        {
            // The transformation engine will only add elements. Anything
            // else that gets added must be contained by a new element.
            // So to determine what's new, we search up the tree for a new
            // element that contains this node.
            return FindContainingElement(node) is XmlFileInfoElement element && !element.IsOriginal;
        }

        static XmlElement FindContainingElement(XmlNode node)
        {
            while (node != null && !(node is XmlElement))
                node = node.ParentNode;
            return node as XmlElement;
        }

        class XmlFileInfoElement : XmlElement, IXmlLineInfo, IXmlFormattableAttributes
        {
            readonly XmlAttributePreservationDict _preservationDict;

            internal XmlFileInfoElement(string prefix, string localName, string namespaceUri, XmlFileInfoDocument document)
                : base(prefix, localName, namespaceUri, document)
            {
                LineNumber = document.CurrentLineNumber;
                LinePosition = document.CurrentLinePosition;
                IsOriginal = document.FirstLoad;

                if (document.PreservationProvider != null)
                    _preservationDict = document.PreservationProvider.GetDictAtPosition(LineNumber, LinePosition - 1);
                if (_preservationDict == null)
                    _preservationDict = new XmlAttributePreservationDict();
            }

            public override void WriteTo(XmlWriter w)
            {
                string prefix = Prefix;
                if (!string.IsNullOrEmpty(NamespaceURI))
                    prefix = w.LookupPrefix(NamespaceURI) ?? Prefix;

                w.WriteStartElement(prefix, LocalName, NamespaceURI);

                if (HasAttributes)
                {
                    var preservingWriter = w as XmlAttributePreservingWriter;
                    if (preservingWriter == null || _preservationDict == null)
                        WriteAttributesTo(w);
                    else
                        WritePreservedAttributesTo(preservingWriter);
                }

                if (IsEmpty)
                    w.WriteEndElement();
                else
                {
                    WriteContentTo(w);
                    w.WriteFullEndElement();
                }
            }

            void WriteAttributesTo(XmlWriter w)
            {
                XmlAttributeCollection attrs = Attributes;
                for (var i = 0; i < attrs.Count; i += 1)
                    attrs[i].WriteTo(w);
            }

            void WritePreservedAttributesTo(XmlAttributePreservingWriter preservingWriter) 
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

        class XmlFileInfoAttribute : XmlAttribute, IXmlLineInfo
        {
            internal XmlFileInfoAttribute(string prefix, string localName, string namespaceUri, XmlFileInfoDocument document)
                : base(prefix, localName, namespaceUri, document)
            {
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
                _reader.Close();
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
            Debug.Fail("Call Dispose please");
            Dispose(false);
        }
    }
}
