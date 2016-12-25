using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;

namespace Microsoft.DotNet.Xdt.Tools
{
    public enum XPathAxis
    {
        Child,
        Descendant,
        Parent,
        Ancestor,
        FollowingSibling,
        PrecedingSibling,
        Following,
        Preceding,
        Self,
        DescendantOrSelf,
        AncestorOrSelf,
    }

    public abstract class Locator
    {
        private IList<string> _arguments;
        private string _parentPath;
        private XmlElementContext _context;
        private XmlTransformationLogger _logger;

        protected virtual string ParentPath => _parentPath;

        protected XmlNode CurrentElement => _context.Element;

        protected virtual string NextStepNodeTest
        {
            get
            {
                if (!string.IsNullOrEmpty(CurrentElement.NamespaceURI) && string.IsNullOrEmpty(CurrentElement.Prefix))
                {
                    return string.Concat("_defaultNamespace:", CurrentElement.LocalName);
                }
                return CurrentElement.Name;
            }
        }
        protected virtual XPathAxis NextStepAxis => XPathAxis.Child;

        protected virtual string ConstructPath() => AppendStep(ParentPath, NextStepAxis, NextStepNodeTest, ConstructPredicate());

        protected string AppendStep(string basePath, string stepNodeTest) => AppendStep(basePath, XPathAxis.Child, stepNodeTest, string.Empty);

        protected string AppendStep(string basePath, XPathAxis stepAxis, string stepNodeTest) => AppendStep(basePath, stepAxis, stepNodeTest, string.Empty);

        protected string AppendStep(string basePath, string stepNodeTest, string predicate) => AppendStep(basePath, XPathAxis.Child, stepNodeTest, predicate);

        protected string AppendStep(string basePath, XPathAxis stepAxis, string stepNodeTest, string predicate)
        {
            return string.Concat(
            EnsureTrailingSlash(basePath), 
            GetAxisString(stepAxis), 
            stepNodeTest, 
            EnsureBracketedPredicate(predicate));
        }

        protected virtual string ConstructPredicate() => string.Empty;

        protected XmlTransformationLogger Log
        {
            get
            {
                if (_logger == null)
                {
                    _logger = _context.GetService<XmlTransformationLogger>();
                    if (_logger != null)
                    {
                        _logger.CurrentReferenceNode = _context.LocatorAttribute;
                    }
                }
                return _logger;
            }
        }

        protected string ArgumentString { get; private set; }

        protected IList<string> Arguments
        {
            get
            {
                if (_arguments == null && ArgumentString != null)
                {
                    _arguments = XmlArgumentUtility.SplitArguments(ArgumentString);
                }
                return _arguments;
            }
        }

        protected void EnsureArguments() => EnsureArguments(1);

        protected void EnsureArguments(int min)
        {
            if (Arguments == null || Arguments.Count < min)
            {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_RequiresMinimumArguments, GetType().Name, min));
            }
        }

        protected void EnsureArguments(int min, int max)
        {
            Debug.Assert(min <= max);
            if (min == max)
            {
                if (Arguments == null || Arguments.Count != min)
                {
                    throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_RequiresExactArguments, GetType().Name, min));
                }
            }

            EnsureArguments(min);

            if (Arguments.Count > max)
            {
                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_TooManyArguments, GetType().Name));
            }
        }

        internal string ConstructPath(string parentPath, XmlElementContext context, string argumentString)
        {
            Debug.Assert(_parentPath == null && _context == null && ArgumentString == null,
                "Do not call ConstructPath recursively");

            string resultPath = string.Empty;

            if (_parentPath == null && _context == null && ArgumentString == null)
            {
                try
                {
                    _parentPath = parentPath;
                    _context = context;
                    ArgumentString = argumentString;

                    resultPath = ConstructPath();
                }
                finally
                {
                    _parentPath = null;
                    _context = null;
                    ArgumentString = null;
                    _arguments = null;

                    ReleaseLogger();
                }
            }

            return resultPath;
        }

        internal string ConstructParentPath(string parentPath, XmlElementContext context, string argumentString)
        {
            Debug.Assert(_parentPath == null && _context == null && ArgumentString == null,
                "Do not call ConstructPath recursively");

            string resultPath = string.Empty;

            if (_parentPath == null && _context == null && ArgumentString == null)
            {
                try
                {
                    _parentPath = parentPath;
                    _context = context;
                    ArgumentString = argumentString;

                    resultPath = ParentPath;
                }
                finally
                {
                    _parentPath = null;
                    _context = null;
                    ArgumentString = null;
                    _arguments = null;

                    ReleaseLogger();
                }
            }

            return resultPath;
        }

        private void ReleaseLogger()
        {
            if (_logger != null)
            {
            _logger.CurrentReferenceNode = null;
            _logger = null;
        }
        }

        private static string GetAxisString(XPathAxis stepAxis)
        {
            switch (stepAxis)
            {
                case XPathAxis.Child:
                    return string.Empty;
                case XPathAxis.Descendant:
                    return "descendant::";
                case XPathAxis.Parent:
                    return "parent::";
                case XPathAxis.Ancestor:
                    return "ancestor::";
                case XPathAxis.FollowingSibling:
                    return "following-sibling::";
                case XPathAxis.PrecedingSibling:
                    return "preceding-sibling::";
                case XPathAxis.Following:
                    return "following::";
                case XPathAxis.Preceding:
                    return "preceding::";
                case XPathAxis.Self:
                    return "self::";
                case XPathAxis.DescendantOrSelf:
                    return "/";
                case XPathAxis.AncestorOrSelf:
                    return "ancestor-or-self::";
                default:
                    Debug.Fail("There should be no XPathAxis enum value that isn't handled in this switch statement");
                    return string.Empty;
            }
        }

        private static string EnsureTrailingSlash(string basePath)
        {
            if (!basePath.EndsWith("/", StringComparison.Ordinal))
            {
                basePath = string.Concat(basePath, "/");
            }

            return basePath;
        }

        private static string EnsureBracketedPredicate(string predicate)
        {
            if (string.IsNullOrEmpty(predicate))
            {
                return string.Empty;
            }
            if (!predicate.StartsWith("[", StringComparison.Ordinal))
            {
                predicate = string.Concat("[", predicate);
            }
            if (!predicate.EndsWith("]", StringComparison.Ordinal))
            {
                predicate = string.Concat(predicate, "]");
            }

            return predicate;
        }
    }
}
