using System;
using System.Diagnostics;
using System.Xml;
using System.IO;
using System.ComponentModel.Design;

namespace Microsoft.DotNet.Xdt.Tools
{
    public class XmlTransformation : IServiceProvider, IDisposable
    {
        internal static readonly string TransformNamespace = "http://schemas.microsoft.com/XML-Document-Transform";
        internal static readonly string SupressWarnings = "SupressWarnings";

        private readonly string _transformFile;

        private XmlDocument _xmlTransformation;
        private XmlDocument _xmlTarget;
        private XmlTransformableDocument _xmlTransformable;

        private readonly XmlTransformationLogger _logger;

        private NamedTypeFactory _namedTypeFactory;
        private ServiceContainer _transformationServiceContainer = new ServiceContainer();
        private ServiceContainer _documentServiceContainer;

        public XmlTransformation(string transformFile)
            : this(transformFile, true, null)
        {
        }

        public XmlTransformation(string transform, IXmlTransformationLogger logger)
            : this(transform, true, logger)
        {
        }

        public XmlTransformation(string transform, bool isTransformAFile, IXmlTransformationLogger logger)
        {
            _transformFile = transform;
            _logger = new XmlTransformationLogger(logger);

            _xmlTransformation = new XmlFileInfoDocument();
            if (isTransformAFile)
            {
                _xmlTransformation.Load(transform);
            }
            else
            {
                _xmlTransformation.LoadXml(transform);
            }

            InitializeTransformationServices();

            PreprocessTransformDocument();
        }

        public XmlTransformation(Stream transformStream, IXmlTransformationLogger logger)
        {
            _logger = new XmlTransformationLogger(logger);
            _transformFile = string.Empty;

            _xmlTransformation = new XmlFileInfoDocument();
            _xmlTransformation.Load(transformStream);

            InitializeTransformationServices();

            PreprocessTransformDocument();
        }

        public bool HasTransformNamespace { get; private set; }

        private void InitializeTransformationServices()
        {
            // Initialize NamedTypeFactory
            _namedTypeFactory = new NamedTypeFactory(_transformFile);
            _transformationServiceContainer.AddService(_namedTypeFactory.GetType(), _namedTypeFactory);

            // Initialize TransformationLogger
            _transformationServiceContainer.AddService(_logger.GetType(), _logger);
        }

        private void InitializeDocumentServices(XmlDocument document)
        {
            Debug.Assert(_documentServiceContainer == null);
            _documentServiceContainer = new ServiceContainer();

            if (document is IXmlOriginalDocumentService)
            {
                _documentServiceContainer.AddService(typeof(IXmlOriginalDocumentService), document);
            }
        }

        private void ReleaseDocumentServices()
        {
            if (_documentServiceContainer != null)
            {
                _documentServiceContainer.RemoveService(typeof(IXmlOriginalDocumentService));
                _documentServiceContainer = null;
            }
        }

        private void PreprocessTransformDocument()
        {
            HasTransformNamespace = false;
            foreach (XmlAttribute attribute in _xmlTransformation.SelectNodes("//namespace::*"))
            {
                if (attribute.Value.Equals(TransformNamespace, StringComparison.Ordinal))
                {
                    HasTransformNamespace = true;
                    break;
                }
            }

            if (HasTransformNamespace)
            {
                // This will look for all nodes from our namespace in the document,
                // and do any initialization work
                var namespaceManager = new XmlNamespaceManager(new NameTable());
                namespaceManager.AddNamespace("xdt", TransformNamespace);
                XmlNodeList namespaceNodes = _xmlTransformation.SelectNodes("//xdt:*", namespaceManager);

                foreach (XmlNode node in namespaceNodes)
                {
                    var element = node as XmlElement;
                    if (element == null)
                    {
                        Debug.Fail("The XPath for elements returned something that wasn't an element?");
                        continue;
                    }

                    XmlElementContext context = null;
                    try
                    {
                        switch (element.LocalName)
                        {
                            case "Import":
                                context = CreateElementContext(null, element);
                                PreprocessImportElement(context);
                                break;
                            default:
                                _logger.LogWarning(element, SR.XMLTRANSFORMATION_UnknownXdtTag, element.Name);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (context != null)
                        {
                            ex = WrapException(ex, context);
                        }

                        _logger.LogErrorFromException(ex);
                        throw new XmlTransformationException(SR.XMLTRANSFORMATION_FatalTransformSyntaxError, ex);
                    }
                    finally
                    {
                        context = null;
                    }
                }
            }
        }

        public void AddTransformationService(Type serviceType, object serviceInstance)
        {
            _transformationServiceContainer.AddService(serviceType, serviceInstance);
        }

        public void RemoveTransformationService(Type serviceType)
        {
            _transformationServiceContainer.RemoveService(serviceType);
        }

        public bool Apply(XmlDocument xmlTarget)
        {
            Debug.Assert(_xmlTarget == null, "This method should not be called recursively");

            if (_xmlTarget == null)
            {
                // Reset the error state
                _logger.HasLoggedErrors = false;

                _xmlTarget = xmlTarget;
                _xmlTransformable = xmlTarget as XmlTransformableDocument;
                try
                {
                    if (HasTransformNamespace)
                    {
                        InitializeDocumentServices(xmlTarget);

                        TransformLoop(_xmlTransformation);
                    }
                    else
                    {
                        _logger.LogMessage(MessageType.Normal, "The expected namespace {0} was not found in the transform file", TransformNamespace);
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex);
                }
                finally
                {
                    ReleaseDocumentServices();

                    _xmlTarget = null;
                    _xmlTransformable = null;
                }

                return !_logger.HasLoggedErrors;
            }
            return false;
        }

        private void TransformLoop(XmlDocument xmlSource)
        {
            TransformLoop(new XmlNodeContext(xmlSource));
        }

        private void TransformLoop(XmlNodeContext parentContext)
        {
            foreach (XmlNode node in parentContext.Node.ChildNodes)
            {
                var element = node as XmlElement;
                if (element == null)
                {
                    continue;
                }

                XmlElementContext context = CreateElementContext(parentContext as XmlElementContext, element);
                try
                {
                    HandleElement(context);
                }
                catch (Exception ex)
                {
                    HandleException(ex, context);
                }
            }
        }

        private XmlElementContext CreateElementContext(XmlElementContext parentContext, XmlElement element)
        {
            return new XmlElementContext(parentContext, element, _xmlTarget, this);
        }

        private void HandleException(Exception ex)
        {
            _logger.LogErrorFromException(ex);
        }

        private void HandleException(Exception ex, XmlNodeContext context)
        {
            HandleException(WrapException(ex, context));
        }

        private static Exception WrapException(Exception ex, XmlNodeContext context)
        {
            return XmlNodeException.Wrap(ex, context.Node);
        }

        private void HandleElement(XmlElementContext context)
        {
            string argumentString;
            Transform transform = context.ConstructTransform(out argumentString);
            if (transform != null)
            {

                bool fOriginalSupressWarning = _logger.SupressWarnings;

                var supressWarningsAttribute = context.Element.Attributes.GetNamedItem(SupressWarnings, TransformNamespace) as XmlAttribute;
                if (supressWarningsAttribute != null)
                {
                    bool fSupressWarning = Convert.ToBoolean(supressWarningsAttribute.Value, System.Globalization.CultureInfo.InvariantCulture);
                    _logger.SupressWarnings = fSupressWarning;
                }

                try
                {
                    OnApplyingTransform();

                    transform.Execute(context, argumentString);

                    OnAppliedTransform();
                }
                catch (Exception ex)
                {
                    HandleException(ex, context);
                }
                finally
                {
                    // reset back the SupressWarnings back per node
                    _logger.SupressWarnings = fOriginalSupressWarning;
                }
            }

            // process children
            TransformLoop(context);
        }

        private void OnApplyingTransform() => _xmlTransformable?.OnBeforeChange();

        private void OnAppliedTransform() => _xmlTransformable?.OnAfterChange();

        private void PreprocessImportElement(XmlElementContext context)
        {
            string assemblyName = null;
            string nameSpace = null;
            string path = null;

            foreach (XmlAttribute attribute in context.Element.Attributes)
            {
                if (attribute.NamespaceURI.Length == 0)
                {
                    switch (attribute.Name)
                    {
                        case "assembly":
                            assemblyName = attribute.Value;
                            continue;
                        case "namespace":
                            nameSpace = attribute.Value;
                            continue;
                        case "path":
                            path = attribute.Value;
                            continue;
                    }
                }

                throw new XmlNodeException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_ImportUnknownAttribute, attribute.Name), attribute);
            }

            if (assemblyName != null && path != null)
            {
                throw new XmlNodeException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_ImportAttributeConflict), context.Element);
            }
            if (assemblyName == null && path == null)
            {
                throw new XmlNodeException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_ImportMissingAssembly), context.Element);
            }
            if (nameSpace == null)
            {
                throw new XmlNodeException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_ImportMissingNamespace), context.Element);
            }
            if (assemblyName != null)
            {
                _namedTypeFactory.AddAssemblyRegistration(assemblyName, nameSpace);
            }
            else
            {
                _namedTypeFactory.AddPathRegistration(path, nameSpace);
            }
        }

        public object GetService(Type serviceType)
        {
            object service = null;
            if (_documentServiceContainer != null)
            {
                service = _documentServiceContainer.GetService(serviceType);
            }
            if (service == null)
            {
                service = _transformationServiceContainer.GetService(serviceType);
            }
            return service;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_transformationServiceContainer != null)
            {
                _transformationServiceContainer.Dispose();
                _transformationServiceContainer = null;
            }

            if (_documentServiceContainer != null)
            {
                _documentServiceContainer.Dispose();
                _documentServiceContainer = null;
            }

            if (_xmlTransformable != null)
            {
                _xmlTransformable.Dispose();
                _xmlTransformable = null;
            }

            var xmlFileInfoDocument = _xmlTransformation as XmlFileInfoDocument;
            if (xmlFileInfoDocument != null)
            {
                xmlFileInfoDocument.Dispose();
                _xmlTransformation = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~XmlTransformation()
        {
            Debug.Fail("call dispose please");
            Dispose(false);
        }
    }
}
