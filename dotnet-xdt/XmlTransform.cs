using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;

namespace DotNet.Xdt
{
    enum MissingTargetMessage
    {
        None,
        Information,
        Warning,
        Error,
    }

    [Flags]
    enum TransformFlags
    {
        None = 0,
        ApplyTransformToAllTargetNodes = 1,
        UseParentAsTargetNode = 2,
    }


    abstract class Transform
    {
        XmlTransformationLogger? _logger;
        XmlElementContext? _context;
        XmlNode? _currentTransformNode;
        XmlNode? _currentTargetNode;

        IList<string>? _arguments;

        protected Transform()
            : this(TransformFlags.None)
        { }

        protected Transform(TransformFlags flags)
            : this(flags, MissingTargetMessage.Warning)
        { }

        protected Transform(TransformFlags flags, MissingTargetMessage message)
        {
            MissingTargetMessage = message;
            ApplyTransformToAllTargetNodes = (flags & TransformFlags.ApplyTransformToAllTargetNodes) == TransformFlags.ApplyTransformToAllTargetNodes;
            UseParentAsTargetNode = (flags & TransformFlags.UseParentAsTargetNode) == TransformFlags.UseParentAsTargetNode;
        }

        protected bool ApplyTransformToAllTargetNodes { get; set; }

        protected bool UseParentAsTargetNode { get; set; }

        protected MissingTargetMessage MissingTargetMessage { get; set; }

        protected abstract void Apply();

        protected XmlNode TransformNode => _currentTransformNode ?? _context!.TransformNode;

        protected XmlNode TargetNode
        {
            get
            {
                if (_currentTargetNode is not null) return _currentTargetNode;

                foreach (XmlNode targetNode in TargetNodes)
                    return targetNode;
                return _currentTargetNode!;
            }
        }

        protected XmlNodeList TargetNodes => UseParentAsTargetNode ? _context!.TargetParents : _context!.TargetNodes;
        
        protected XmlNodeList TargetChildNodes => _context!.TargetNodes;

        protected XmlTransformationLogger Log
        {
            get
            {
                if (_logger is null)
                {
                    _logger = _context!.GetService<XmlTransformationLogger>();
                    if (_logger is not null)
                        _logger.CurrentReferenceNode = _context.TransformAttribute;
                }
                return _logger!;
            }
        }

        protected T? GetService<T>() where T : class => _context!.GetService<T>();

        protected string? ArgumentString { get; private set; }

        protected IList<string> Arguments
        {
            get
            {
                if (_arguments is null && ArgumentString is not null)
                    _arguments = XmlArgumentUtility.SplitArguments(ArgumentString);
                return _arguments!;
            }
        }

        string TransformNameLong
        {
            get
            {
                if (_context!.HasLineInfo)
                    return string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_TransformNameFormatLong, TransformName, _context.TransformLineNumber, _context.TransformLinePosition);
                return TransformNameShort;
            }
        }

        internal string TransformNameShort 
            => string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_TransformNameFormatShort, TransformName);

        string TransformName => GetType().Name;

        internal void Execute(XmlElementContext context, string? argumentString)
        {
            Debug.Assert(_context is null && ArgumentString is null, "Don't call Execute recursively");
            Debug.Assert(_logger is null, "Logger wasn't released from previous execution");

            if (_context is null && ArgumentString is null)
            {
                var error = false;
                var startedSection = false;

                try
                {
                    _context = context;
                    ArgumentString = argumentString;
                    _arguments = null;

                    if (ShouldExecuteTransform())
                    {
                        startedSection = true;

                        Log.StartSection(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformBeginExecutingMessage, TransformNameLong);
                        Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformStatusXPath, context.XPath);

                        if (ApplyTransformToAllTargetNodes)
                            ApplyOnAllTargetNodes();
                        else
                            ApplyOnce();
                    }
                }
                catch (Exception ex)
                {
                    error = true;
                    Log.LogErrorFromException(context.TransformAttribute is not null 
                        ? XmlNodeException.Wrap(ex, context.TransformAttribute) 
                        : ex);
                }
                finally
                {
                    if (startedSection)
                    {
                        Log.EndSection(MessageType.Verbose, error
                            ? SR.XMLTRANSFORMATION_TransformErrorExecutingMessage
                            : SR.XMLTRANSFORMATION_TransformEndExecutingMessage, TransformNameShort);
                    }
                    else
                        Log.LogMessage(MessageType.Normal, SR.XMLTRANSFORMATION_TransformNotExecutingMessage, TransformNameLong);

                    _context = null;
                    ArgumentString = null;
                    _arguments = null;

                    ReleaseLogger();
                }
            }
        }

        void ReleaseLogger()
        {
            if (_logger != null)
            {
                _logger.CurrentReferenceNode = null;
                _logger = null;
            }
        }

        void ApplyOnAllTargetNodes()
        {
            XmlNode? originalTransformNode = TransformNode;

            foreach (XmlNode node in TargetNodes)
            {
                try
                {
                    _currentTargetNode = node;
                    _currentTransformNode = originalTransformNode!.Clone();

                    ApplyOnce();
                }
                catch (Exception ex)
                {
                    Log.LogErrorFromException(ex);
                }
            }

            _currentTargetNode = null;
        }

        void ApplyOnce()
        {
            WriteApplyMessage(TargetNode);
            Apply();
        }

        void WriteApplyMessage(XmlNode? targetNode)
        {
            if (targetNode is IXmlLineInfo lineInfo)
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformStatusApplyTarget, targetNode.Name, lineInfo.LineNumber, lineInfo.LinePosition);
            else
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformStatusApplyTargetNoLineInfo, targetNode!.Name);
        }

        bool ShouldExecuteTransform() => HasRequiredTarget();

        bool HasRequiredTarget()
        {
            bool hasRequiredTarget = UseParentAsTargetNode
                ? _context!.HasTargetParent(out XmlElementContext? matchFailureContext, out bool existedInOriginal)
                : _context!.HasTargetNode(out matchFailureContext, out existedInOriginal);

            if (hasRequiredTarget) return true;

            HandleMissingTarget(matchFailureContext, existedInOriginal);
            return false;
        }

        void HandleMissingTarget(XmlElementContext? matchFailureContext, bool existedInOriginal)
        {
            string messageFormat = existedInOriginal
                ? SR.XMLTRANSFORMATION_TransformSourceMatchWasRemoved
                : SR.XMLTRANSFORMATION_TransformNoMatchingTargetNodes;

            string message = string.Format(System.Globalization.CultureInfo.CurrentCulture, messageFormat, matchFailureContext!.XPath);
            switch (MissingTargetMessage)
            {
                case MissingTargetMessage.None:
                    Log.LogMessage(MessageType.Verbose, message);
                    break;
                case MissingTargetMessage.Information:
                    Log.LogMessage(MessageType.Normal, message);
                    break;
                case MissingTargetMessage.Warning:
                    Log.LogWarning(matchFailureContext.Node, message);
                    break;
                case MissingTargetMessage.Error:
                    throw new XmlNodeException(message, matchFailureContext.Node);
            }
        }
    }
}
