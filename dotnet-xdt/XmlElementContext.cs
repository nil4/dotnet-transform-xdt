using System;
using System.Collections.Generic;
using System.Xml;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;

namespace DotNet.Xdt
{
    class XmlElementContext : XmlNodeContext
    {
        readonly XmlElementContext? _parentContext;
        string? _xpath;
        string? _parentXPath;

        readonly IServiceProvider _serviceProvider;

        XmlNode? _transformNodes;
        XmlNodeList? _targetNodes;
        XmlNodeList? _targetParents;

        XmlAttribute? _transformAttribute;
        XmlAttribute? _locatorAttribute;

        XmlNamespaceManager? _namespaceManager;

        public XmlElementContext(XmlElementContext? parent, XmlElement element, XmlDocument xmlTargetDoc, IServiceProvider serviceProvider)
            : base(element)
        {
            _parentContext = parent;
            TargetDocument = xmlTargetDoc;
            _serviceProvider = serviceProvider;
        }

        public T? GetService<T>() where T : class
        {
            if (_serviceProvider != null)
            {
                // note it is legal to return service that's null -- due to SetTokenizeAttributeStorage
                return _serviceProvider.GetService(typeof(T)) as T;
            }
            
            Debug.Fail("No ServiceProvider");
            return null;
        }

        public XmlElement? Element => Node as XmlElement;

        public string XPath => _xpath ??= ConstructXPath();

        public string ParentXPath => _parentXPath ??= ConstructParentXPath();

        public Transform? ConstructTransform(out string? argumentString)
        {
            try
            {
                return CreateObjectFromAttribute<Transform>(out argumentString, out _transformAttribute);
            }
            catch (Exception ex)
            {
                throw WrapException(ex);
            }
        }

        public int TransformLineNumber => (_transformAttribute as IXmlLineInfo)?.LineNumber ?? LineNumber;

        public int TransformLinePosition => (_transformAttribute as IXmlLineInfo)?.LinePosition ?? LinePosition;

        public XmlAttribute? TransformAttribute => _transformAttribute;

        public XmlAttribute? LocatorAttribute => _locatorAttribute;

        string ConstructXPath()
        {
            try
            {
                string parentPath = _parentContext == null ? string.Empty : _parentContext.XPath;

                Locator locator = CreateLocator(out string? argumentString);

                return locator.ConstructPath(parentPath, this, argumentString);
            }
            catch (Exception ex)
            {
                throw WrapException(ex);
            }
        }

        string ConstructParentXPath()
        {
            try
            {
                string parentPath = _parentContext == null ? string.Empty : _parentContext.XPath;

                Locator locator = CreateLocator(out string? argumentString);

                return locator.ConstructParentPath(parentPath, this, argumentString);
            }
            catch (Exception ex)
            {
                throw WrapException(ex);
            }
        }

        Locator CreateLocator(out string? argumentString)
        {
            var locator = CreateObjectFromAttribute<Locator>(out argumentString, out _locatorAttribute);
            if (locator == null)
            {
                argumentString = null;
                //avoid using singleton of "DefaultLocator.Instance", so unit tests can run parallel
                locator = new DefaultLocator();
            }
            return locator;
        }

        internal XmlNode TransformNode => _transformNodes ??= CreateCloneInTargetDocument(Element);

        internal XmlNodeList TargetNodes => _targetNodes ??= GetTargetNodes(XPath);

        internal XmlNodeList TargetParents
        {
            get
            {
                if (_targetParents == null && _parentContext != null)
                    _targetParents = GetTargetNodes(ParentXPath);
                return _targetParents!;
            }
        }

        XmlDocument TargetDocument { get; }

        XmlNode CreateCloneInTargetDocument(XmlNode? sourceNode)
        {
            var infoDocument = TargetDocument as XmlFileInfoDocument;
            XmlNode clonedNode;

            if (infoDocument != null)
                clonedNode = infoDocument.CloneNodeFromOtherDocument(sourceNode);
            else
            {
                var reader = new XmlTextReader(new StringReader(sourceNode?.OuterXml));
                clonedNode = TargetDocument.ReadNode(reader);
            }

            ScrubTransformAttributesAndNamespaces(clonedNode);

            return clonedNode;
        }

        static void ScrubTransformAttributesAndNamespaces(XmlNode node)
        {
            if (node.Attributes != null)
            {
                var attributesToRemove = new List<XmlAttribute>();

                foreach (XmlAttribute attribute in node.Attributes)
                {
                    if (attribute.NamespaceURI == XmlTransformation.TransformNamespace)
                        attributesToRemove.Add(attribute);
                    else if (attribute.Prefix!.Equals("xmlns") || attribute.Name.Equals("xmlns"))
                        attributesToRemove.Add(attribute);
                    else
                        attribute.Prefix = null;
                }

                foreach (XmlAttribute attributeToRemove in attributesToRemove)
                    node.Attributes.Remove(attributeToRemove);
            }

            // Do the same recursively for child nodes
            foreach (XmlNode childNode in node.ChildNodes)
                ScrubTransformAttributesAndNamespaces(childNode);
        }

        XmlNodeList GetTargetNodes(string xpath) => TargetDocument.SelectNodes(xpath, GetNamespaceManager());

        Exception WrapException(Exception ex) => XmlNodeException.Wrap(ex, Element!);

        static Exception WrapException(Exception ex, XmlNode? node) => XmlNodeException.Wrap(ex, node!);

        XmlNamespaceManager GetNamespaceManager()
        {
            if (_namespaceManager == null)
            {
                XmlNodeList localNamespaces = Element!.SelectNodes("namespace::*");

                if (localNamespaces.Count > 0)
                {
                    _namespaceManager = new XmlNamespaceManager(Element.OwnerDocument.NameTable);

                    foreach (XmlAttribute nsAttribute in localNamespaces)
                    {
                        int index = nsAttribute.Name.IndexOf(':');
                        string prefix = index >= 0
                            ? nsAttribute.Name.Substring(index + 1)
                            : "_defaultNamespace";

                        _namespaceManager.AddNamespace(prefix, nsAttribute.Value);
                    }
                }
                else
                    _namespaceManager = new XmlNamespaceManager(GetParentNameTable());
            }
            return _namespaceManager;
        }

        XmlNameTable? GetParentNameTable() 
            => _parentContext == null ? Element?.OwnerDocument?.NameTable : _parentContext.GetNamespaceManager().NameTable;

        static Regex _nameAndArgumentsRegex;
        static Regex NameAndArgumentsRegex => _nameAndArgumentsRegex ??= new Regex(@"\A\s*(?<name>\w+)(\s*\((?<arguments>.*)\))?\s*\Z", RegexOptions.Compiled | RegexOptions.Singleline);

        static string ParseNameAndArguments(string name, out string? arguments)
        {
            arguments = null;

            System.Text.RegularExpressions.Match match = NameAndArgumentsRegex.Match(name);
            if (match.Success)
            {
                if (match.Groups["arguments"].Success)
                {
                    CaptureCollection argumentCaptures = match.Groups["arguments"].Captures;
                    if (argumentCaptures.Count == 1 && !string.IsNullOrEmpty(argumentCaptures[0].Value))
                        arguments = argumentCaptures[0].Value;
                }

                return match.Groups["name"].Captures[0].Value;
            }
            throw new XmlTransformationException(SR.XMLTRANSFORMATION_BadAttributeValue);
        }

        TObjectType? CreateObjectFromAttribute<TObjectType>(out string? argumentString, out XmlAttribute? objectAttribute) 
            where TObjectType : class
        {
            objectAttribute = Element?.Attributes.GetNamedItem(typeof(TObjectType).Name, XmlTransformation.TransformNamespace) as XmlAttribute;
            try
            {
                if (objectAttribute != null)
                {
                    string typeName = ParseNameAndArguments(objectAttribute.Value, out argumentString);
                    if (!string.IsNullOrEmpty(typeName))
                        return GetService<NamedTypeFactory>()!.Construct<TObjectType>(typeName);
                }
            }
            catch (Exception ex)
            {
                throw WrapException(ex, objectAttribute);
            }

            argumentString = null;
            return null;
        }

        internal bool HasTargetNode(out XmlElementContext? failedContext, out bool existedInOriginal)
        {
            failedContext = null;
            existedInOriginal = false;

            if (TargetNodes.Count == 0)
            {
                failedContext = this;
                while (failedContext._parentContext != null && failedContext._parentContext.TargetNodes.Count == 0)
                    failedContext = failedContext._parentContext;

                existedInOriginal = ExistedInOriginal(failedContext.XPath);
                return false;
            }

            return true;
        }

        internal bool HasTargetParent(out XmlElementContext failedContext, out bool existedInOriginal)
        {
            failedContext = null!;
            existedInOriginal = false;

            if (TargetParents.Count == 0)
            {
                failedContext = this;
                while (!string.IsNullOrEmpty(failedContext._parentContext?.ParentXPath) && failedContext._parentContext.TargetParents.Count == 0)
                    failedContext = failedContext._parentContext;

                existedInOriginal = ExistedInOriginal(failedContext.XPath);
                return false;
            }

            return true;
        }

        bool ExistedInOriginal(string xpath)
        {
            var service = GetService<IXmlOriginalDocumentService>();
            XmlNodeList? nodeList = service?.SelectNodes(xpath, GetNamespaceManager());
            return nodeList != null && nodeList.Count > 0;
        }
    }
}
