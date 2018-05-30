using System.Text;
using System.IO;

namespace DotNet.Xdt
{
    class PositionTrackingTextReader : TextReader
    {
        readonly TextReader _internalReader;

        int _lineNumber = 1;
        int _linePosition = 1;
        int _characterPosition = 1;

        const int NewlineCharacter = '\n';

        public PositionTrackingTextReader(TextReader textReader) 
            => _internalReader = textReader;

        public override int Read()
        {
            int read = _internalReader.Read();

            UpdatePosition(read);

            return read;
        }

        public override int Peek() => _internalReader.Peek();

        public bool ReadToPosition(int lineNumber, int linePosition)
        {
            while (_lineNumber < lineNumber && Peek() != -1)
                ReadLine();

            while (_linePosition < linePosition && Peek() != -1)
                Read();

            return _lineNumber == lineNumber && _linePosition == linePosition;
        }

        public bool ReadToPosition(int characterPosition)
        {
            while (_characterPosition < characterPosition && Peek() != -1)
                Read();

            return _characterPosition == characterPosition;
        }

        void UpdatePosition(int character)
        {
            if (character == NewlineCharacter)
            {
                _lineNumber++;
                _linePosition = 1;
            }
            else
                _linePosition++;

            _characterPosition++;
        }
    }

    class WhitespaceTrackingTextReader : PositionTrackingTextReader
    {
        readonly StringBuilder _precedingWhitespace = new StringBuilder();

        public WhitespaceTrackingTextReader(TextReader reader)
            : base(reader)
        { }

        public override int Read()
        {
            int read = base.Read();

            UpdateWhitespaceTracking((char)read);

            return read;
        }

        public string PrecedingWhitespace => _precedingWhitespace.ToString();

        void UpdateWhitespaceTracking(char character)
        {
            if (char.IsWhiteSpace(character))
                AppendWhitespaceCharacter(character);
            else
                ResetWhitespaceString();
        }

        void AppendWhitespaceCharacter(char character) => _precedingWhitespace.Append(character);

        void ResetWhitespaceString() => _precedingWhitespace.Clear();
    }
}
