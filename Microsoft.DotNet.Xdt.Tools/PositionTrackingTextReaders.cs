using System.Text;
using System.IO;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal class PositionTrackingTextReader : TextReader
    {
        private readonly TextReader _internalReader;

        private int _lineNumber = 1;
        private int _linePosition = 1;
        private int _characterPosition = 1;

        private const int NewlineCharacter = '\n';

        public PositionTrackingTextReader(TextReader textReader) {
            _internalReader = textReader;
        }

        public override int Read() {
            int read = _internalReader.Read();

            UpdatePosition(read);

            return read;
        }

        public override int Peek() {
            return _internalReader.Peek();
        }

        public bool ReadToPosition(int lineNumber, int linePosition) {
            while (_lineNumber < lineNumber && Peek() != -1) {
                ReadLine();
            }

            while (_linePosition < linePosition && Peek() != -1) {
                Read();
            }

            return _lineNumber == lineNumber && _linePosition == linePosition;
        }

        public bool ReadToPosition(int characterPosition) {
            while (_characterPosition < characterPosition && Peek() != -1) {
                Read();
            }

            return _characterPosition == characterPosition;
        }

        private void UpdatePosition(int character) {
            if (character == NewlineCharacter) {
                _lineNumber++;
                _linePosition = 1;
            }
            else {
                _linePosition++;
            }
            _characterPosition++;
        }
    }

    internal class WhitespaceTrackingTextReader : PositionTrackingTextReader
    {
        private StringBuilder precedingWhitespace = new StringBuilder();

        public WhitespaceTrackingTextReader(TextReader reader)
            : base(reader) {
        }

        public override int Read() {
            int read = base.Read();

            UpdateWhitespaceTracking(read);

            return read;
        }

        public string PrecedingWhitespace => precedingWhitespace.ToString();

        private void UpdateWhitespaceTracking(int character) {
            if (char.IsWhiteSpace((char)character)) {
                AppendWhitespaceCharacter(character);
            }
            else {
                ResetWhitespaceString();
            }
        }

        private void AppendWhitespaceCharacter(int character) {
            precedingWhitespace.Append((char)character);
        }

        private void ResetWhitespaceString() {
            precedingWhitespace = new StringBuilder();
        }
    }
}
