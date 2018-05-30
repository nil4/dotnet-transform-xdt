using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;

namespace DotNet.Xdt
{
    public abstract class AttributeTransform : Transform
    {
        XmlNode _transformAttributeSource;
        XmlNodeList _transformAttributes;
        XmlNode _targetAttributeSource;
        XmlNodeList _targetAttributes;

        protected AttributeTransform()
            : base(TransformFlags.ApplyTransformToAllTargetNodes)
        { }

        protected XmlNodeList TransformAttributes
        {
            get
            {
                if (_transformAttributes == null || _transformAttributeSource != TransformNode)
                {
                    _transformAttributeSource = TransformNode;
                    _transformAttributes = GetAttributesFrom(TransformNode);
                }
                return _transformAttributes;
            }
        }

        protected XmlNodeList TargetAttributes
        {
            get
            {
                if (_targetAttributes == null || _targetAttributeSource != TargetNode)
                {
                    _targetAttributeSource = TargetNode;
                    _targetAttributes = GetAttributesFrom(TargetNode);
                }
                return _targetAttributes;
            }
        }

        XmlNodeList GetAttributesFrom(XmlNode node)
        {
            if (Arguments == null || Arguments.Count == 0)
                return GetAttributesFrom(node, "*", false);

            if (Arguments.Count == 1)
                return GetAttributesFrom(node, Arguments[0], true);
            
            // First verify all the arguments
            foreach (string argument in Arguments)
                GetAttributesFrom(node, argument, true);

            // Now return the complete XPath and return the combined list
            return GetAttributesFrom(node, Arguments, false);
        }

        XmlNodeList GetAttributesFrom(XmlNode node, string argument, bool warnIfEmpty) 
            => GetAttributesFrom(node, new[] { argument }, warnIfEmpty);

        XmlNodeList GetAttributesFrom(XmlNode node, IList<string> arguments, bool warnIfEmpty)
        {
            var array = new string[arguments.Count];
            arguments.CopyTo(array, 0);
            string xpath = string.Concat("@", string.Join("|@", array));
            
            XmlNodeList attributes = node.SelectNodes(xpath);
            if (attributes.Count == 0 && warnIfEmpty)
            {
                Debug.Assert(arguments.Count == 1, "Should only call warnIfEmpty==true with one argument");
                if (arguments.Count == 1)
                    Log.LogWarning(SR.XMLTRANSFORMATION_TransformArgumentFoundNoAttributes, arguments[0]);
            }

            return attributes;
        }
    }
}
