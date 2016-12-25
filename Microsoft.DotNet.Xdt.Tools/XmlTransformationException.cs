using System;

namespace Microsoft.DotNet.Xdt.Tools
{
    // This doesn't do anything, except mark an error as having come from
    // the transformation engine
#if NET451
    [Serializable]
#endif
    public class XmlTransformationException : Exception
    {
        public XmlTransformationException(string message)
            : base(message)
        {
        }

        public XmlTransformationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
