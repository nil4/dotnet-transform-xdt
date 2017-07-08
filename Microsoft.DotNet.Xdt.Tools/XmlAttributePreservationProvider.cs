using System;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal class XmlAttributePreservationProvider : IDisposable
    {
        private StreamReader _streamReader;
        private PositionTrackingTextReader _reader;

        public XmlAttributePreservationProvider(string fileName)
        {
            _streamReader = new StreamReader(File.OpenRead(fileName));
            _reader = new PositionTrackingTextReader(_streamReader);
        }

        public XmlAttributePreservationDict GetDictAtPosition(int lineNumber, int linePosition)
        {
            if (_reader.ReadToPosition(lineNumber, linePosition))
            {
                Debug.Assert((char)_reader.Peek() == '<');

                var sb = new StringBuilder();
                int character;
                var inAttribute = false;
                do
                {
                    character = _reader.Read();
                    if (character == '\"')
                    {
                        inAttribute = !inAttribute;
                    }
                    sb.Append((char)character);
                }
                while (character > 0 && ((char)character != '>' || inAttribute));

                if (character > 0)
                {
                    var dict = new XmlAttributePreservationDict();
                    dict.ReadPreservationInfo(sb.ToString());
                    return dict;
                }
            }

            Debug.Fail("Failed to get preservation info");
            return null;
        }

        public void Close()
        {
            Dispose();
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            if (_streamReader != null)
            {
                _streamReader.Close();
                _streamReader.Dispose();
                _streamReader = null;
            }
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
        }

        ~XmlAttributePreservationProvider()
        {
            Debug.Fail("call dispose please");
            Dispose();
        }
    }
}
