using System;
using System.Xml;
using System.Globalization;

namespace DotNet.Xdt
{
    sealed class DefaultLocator : Locator
    {
        internal static DefaultLocator Instance { get; } = new DefaultLocator();
    }

    public sealed class Match : Locator
    {
        protected override string ConstructPredicate()
        {
            EnsureArguments(1);

            string keyPredicate = null;

            foreach (string key in Arguments)
            {
                if (CurrentElement.Attributes.GetNamedItem(key) is XmlAttribute keyAttribute)
                {
                    string keySegment = string.Format(CultureInfo.InvariantCulture, "@{0}='{1}'", keyAttribute.Name, keyAttribute.Value);
                    keyPredicate = keyPredicate == null ? keySegment : string.Concat(keyPredicate, " and ", keySegment);
                }
                else
                    throw new XmlTransformationException(string.Format(CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_MatchAttributeDoesNotExist, key));
            }

            return keyPredicate;
        }
    }

    public sealed class Condition : Locator
    {
        protected override string ConstructPredicate()
        {
            EnsureArguments(1, 1);
            return Arguments[0];
        }
    }

    public sealed class XPath : Locator
    {
        protected override string ParentPath => ConstructPath();

        protected override string ConstructPath()
        {
            EnsureArguments(1, 1);

            string xpath = Arguments[0];
            if (!xpath.StartsWith("/", StringComparison.Ordinal))
            {
                // Relative XPath
                xpath = AppendStep(base.ParentPath, NextStepNodeTest);
                xpath = AppendStep(xpath, Arguments[0]);
                xpath = xpath.Replace("/./", "/");
            }

            return xpath;
        }
    }
}
