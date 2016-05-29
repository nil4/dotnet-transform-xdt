using System;
using System.Text;
using System.Xml;
using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal class XmlAttributePreservingWriter : XmlWriter
    {
        private readonly XmlWriter _xmlWriter;
        private readonly AttributeTextWriter _textWriter;

        public XmlAttributePreservingWriter(Stream w, Encoding encoding)
            : this(encoding == null ? new StreamWriter(w) : new StreamWriter(w, encoding))
        {
        }

        public XmlAttributePreservingWriter(TextWriter textWriter) {
            _textWriter = new AttributeTextWriter(textWriter);
            _xmlWriter = Create(_textWriter);
        }

        public void WriteAttributeWhitespace(string whitespace) {
            Debug.Assert(IsOnlyWhitespace(whitespace), "Expected only whitespace");

            // Make sure we're in the right place to write
            // whitespace between attributes
            if (WriteState == WriteState.Attribute) {
                WriteEndAttribute();
            }
            else if (WriteState != WriteState.Element) {
                throw new InvalidOperationException();
            }

            // We don't write right away. We're going to wait until an
            // attribute is being written
            _textWriter.AttributeLeadingWhitespace = whitespace;
        }

        public void WriteAttributeTrailingWhitespace(string whitespace) {
            Debug.Assert(IsOnlyWhitespace(whitespace), "Expected only whitespace");

            if (WriteState == WriteState.Attribute) {
                WriteEndAttribute();
            }
            else if (WriteState != WriteState.Element) {
                throw new InvalidOperationException();
            }

            _textWriter.Write(whitespace);
        }

        public string SetAttributeNewLineString(string newLineString) {
            string old = _textWriter.AttributeNewLineString;

            if (newLineString == null && _xmlWriter.Settings != null) {
                newLineString = _xmlWriter.Settings.NewLineChars;
            }
            if (newLineString == null) {
                newLineString = "\r\n";
            }
            _textWriter.AttributeNewLineString = newLineString;

            return old;
        }

        private static bool IsOnlyWhitespace(string whitespace) {
            foreach (char whitespaceCharacter in whitespace) {
                if (!char.IsWhiteSpace(whitespaceCharacter)) {
                    return false;
                }
            }
            return true;
        }

        private class AttributeTextWriter : TextWriter
        {
            private enum State
            {
                Writing,
                WaitingForAttributeLeadingSpace,
                ReadingAttribute,
                Buffering,
                FlushingBuffer,
            }

            private State _state = State.Writing;
            private StringBuilder _writeBuffer;

            private readonly TextWriter _baseWriter;
            private string _leadingWhitespace;

            private int _linePosition = 1;
            private int _lineNumber = 1;

            public AttributeTextWriter(TextWriter baseWriter)
                : base(CultureInfo.InvariantCulture) {
                _baseWriter = baseWriter;
            }

            public string AttributeLeadingWhitespace {
                set {
                    _leadingWhitespace = value;
                }
            }

            public string AttributeNewLineString { get; set; } = "\r\n";

            public void StartAttribute() {
                Debug.Assert(_state == State.Writing, "Expected state=Writing but found: " + _state);

                ChangeState(State.WaitingForAttributeLeadingSpace);
            }

            public void EndAttribute() {
                //Debug.Assert(_state == State.ReadingAttribute, "Expected state=ReadingAttr but found: " + _state);

                WriteQueuedAttribute();
            }

            private int MaxLineLength { get; } = 160;

            public override void Write(char value) {
                UpdateState(value);

                switch (_state) {
                    case State.WaitingForAttributeLeadingSpace:
                        if (value == ' ') {
                            ChangeState(State.ReadingAttribute);
                            break;
                        }
                        goto case State.Writing;
                    case State.Writing:
                    case State.FlushingBuffer:
                        ReallyWriteCharacter(value);
                        break;
                    case State.ReadingAttribute:
                    case State.Buffering:
                        _writeBuffer.Append(value);
                        break;
                }
            }

            private void UpdateState(char value) {
                // This logic prevents writing the leading space that
                // XmlTextWriter wants to put before "/>". 
                switch (value) {
                    case ' ':
                        if (_state == State.Writing) {
                            ChangeState(State.Buffering);
                        }
                        break;
                    case '/':
                        break;
                    case '>':
                        if (_state == State.Buffering) {
                            string currentBuffer = _writeBuffer.ToString();
                            if (currentBuffer.EndsWith(" /", StringComparison.Ordinal)) {
                                // We've identified the string " />" at the
                                // end of the buffer, so remove the space
                                _writeBuffer.Remove(currentBuffer.LastIndexOf(' '), 1);
                            }
                            ChangeState(State.Writing);
                        }
                        break;
                    default:
                        if (_state == State.Buffering) {
                            ChangeState(State.Writing);
                        }
                        break;
                }
            }

            private void ChangeState(State newState) {
                if (_state == newState) return;
                State oldState = _state;
                _state = newState;

                // Handle buffer management for different states
                if (StateRequiresBuffer(newState)) {
                    CreateBuffer();
                }
                else if (StateRequiresBuffer(oldState)) {
                    FlushBuffer();
                }
            }

            private static bool StateRequiresBuffer(State state) 
                => state == State.Buffering || state == State.ReadingAttribute;

            private void CreateBuffer() {
                Debug.Assert(_writeBuffer == null);
                if (_writeBuffer == null) {
                    _writeBuffer = new StringBuilder();
                }
            }

            private void FlushBuffer() {
                Debug.Assert(_writeBuffer != null);
                if (_writeBuffer == null) return;
                State oldState = _state;
                try {
                    _state = State.FlushingBuffer;

                    Write(_writeBuffer.ToString());
                    _writeBuffer = null;
                }
                finally {
                    _state = oldState;
                }
            }

            private void ReallyWriteCharacter(char value) {
                _baseWriter.Write(value);

                if (value == '\n') {
                    _lineNumber++;
                    _linePosition = 1;
                }
                else {
                    _linePosition++;
                }
            }

            private void WriteQueuedAttribute() {
                // Write leading whitespace
                Debug.Assert(_writeBuffer != null, "_writeBuffer was null");
                if (_leadingWhitespace != null) {
                    _writeBuffer.Insert(0, _leadingWhitespace);
                    _leadingWhitespace = null;
                }
                else {
                    int lineLength = _linePosition + _writeBuffer.Length + 1;
                    if (lineLength > MaxLineLength) {
                        _writeBuffer.Insert(0, AttributeNewLineString);
                    }
                    else {
                        _writeBuffer.Insert(0, ' ');
                    }
                }

                // Flush the buffer and start writing characters again
                ChangeState(State.Writing);
            }

            public override Encoding Encoding => _baseWriter.Encoding;

            public override void Flush() => _baseWriter.Flush();
        }

        public override void Flush() 
            => _xmlWriter.Flush();

        public override string LookupPrefix(string ns) 
            => _xmlWriter.LookupPrefix(ns);

        public override void WriteBase64(byte[] buffer, int index, int count) 
            => _xmlWriter.WriteBase64(buffer, index, count);

        public override void WriteCData(string text) 
            => _xmlWriter.WriteCData(text);

        public override void WriteCharEntity(char ch) 
            => _xmlWriter.WriteCharEntity(ch);

        public override void WriteChars(char[] buffer, int index, int count) 
            => _xmlWriter.WriteChars(buffer, index, count);

        public override void WriteComment(string text) 
            => _xmlWriter.WriteComment(text);

        public override void WriteDocType(string name, string pubid, string sysid, string subset) 
            => _xmlWriter.WriteDocType(name, pubid, sysid, subset);

        public override void WriteEndAttribute() {
            _xmlWriter.WriteEndAttribute();
            _textWriter.EndAttribute();
        }

        public override void WriteEndDocument() 
            => _xmlWriter.WriteEndDocument();

        public override void WriteEndElement() 
            => _xmlWriter.WriteEndElement();

        public override void WriteEntityRef(string name) 
            => _xmlWriter.WriteEntityRef(name);

        public override void WriteFullEndElement() 
            => _xmlWriter.WriteFullEndElement();

        public override void WriteProcessingInstruction(string name, string text) 
            => _xmlWriter.WriteProcessingInstruction(name, text);

        public override void WriteRaw(string data) 
            => _xmlWriter.WriteRaw(data);

        public override void WriteRaw(char[] buffer, int index, int count) 
            => _xmlWriter.WriteRaw(buffer, index, count);

        public override void WriteStartAttribute(string prefix, string localName, string ns) {
            _textWriter.StartAttribute();
            _xmlWriter.WriteStartAttribute(prefix, localName, ns);
        }

        public override void WriteStartDocument(bool standalone) 
            => _xmlWriter.WriteStartDocument(standalone);

        public override void WriteStartDocument() 
            => _xmlWriter.WriteStartDocument();

        public override void WriteStartElement(string prefix, string localName, string ns) 
            => _xmlWriter.WriteStartElement(prefix, localName, ns);

        public override WriteState WriteState 
            => _xmlWriter.WriteState;

        public override void WriteString(string text) 
            => _xmlWriter.WriteString(text);

        public override void WriteSurrogateCharEntity(char lowChar, char highChar) 
            => _xmlWriter.WriteSurrogateCharEntity(lowChar, highChar);

        public override void WriteWhitespace(string ws) 
            => _xmlWriter.WriteWhitespace(ws);
    }
}
