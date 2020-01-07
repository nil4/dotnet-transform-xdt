using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Diagnostics;
using RegularExpressions = System.Text.RegularExpressions;

namespace DotNet.Xdt
{
    static class CommonErrors
    {
        internal static void ExpectNoArguments(XmlTransformationLogger log, string transformName, string? argumentString)
        {
            if (!string.IsNullOrEmpty(argumentString))
                log.LogWarning(SR.XMLTRANSFORMATION_TransformDoesNotExpectArguments, transformName);
        }

        internal static void WarnIfMultipleTargets(XmlTransformationLogger log, string transformName, XmlNodeList targetNodes, bool applyTransformToAllTargets)
        {
            Debug.Assert(applyTransformToAllTargets == false);

            if (targetNodes.Count > 1)
                log.LogWarning(SR.XMLTRANSFORMATION_TransformOnlyAppliesOnce, transformName);
        }
    }

    class Replace : Transform
    {
        protected override void Apply()
        {
            CommonErrors.ExpectNoArguments(Log, TransformNameShort, ArgumentString);
            CommonErrors.WarnIfMultipleTargets(Log, TransformNameShort, TargetNodes, ApplyTransformToAllTargetNodes);

            TargetNode.ParentNode.ReplaceChild(TransformNode, TargetNode);

            Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageReplace, TargetNode.Name);
        }
    }


    class Remove : Transform
    {
        protected override void Apply()
        {
            CommonErrors.WarnIfMultipleTargets(Log, TransformNameShort, TargetNodes, ApplyTransformToAllTargetNodes);

            RemoveNode();
        }

        protected void RemoveNode()
        {
            CommonErrors.ExpectNoArguments(Log, TransformNameShort, ArgumentString);

            TargetNode.ParentNode.RemoveChild(TargetNode);

            Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageRemove, TargetNode.Name);
        }
    }

    class RemoveAll : Remove
    {
        public RemoveAll() => ApplyTransformToAllTargetNodes = true;

        protected override void Apply() => RemoveNode();
    }

    class Insert : Transform
    {
        public Insert()
            : base(TransformFlags.UseParentAsTargetNode, MissingTargetMessage.Error)
        { }

        protected override void Apply()
        {
            CommonErrors.ExpectNoArguments(Log, TransformNameShort, ArgumentString);

            TargetNode.AppendChild(TransformNode);

            Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageInsert, TransformNode.Name);
        }
    }

    class InsertIfMissing : Insert
    {
        protected override void Apply()
        {
            CommonErrors.ExpectNoArguments(Log, TransformNameShort, ArgumentString);
            if (TargetChildNodes == null || TargetChildNodes.Count == 0)
            {
                TargetNode.AppendChild(TransformNode);
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageInsert, TransformNode.Name);
            }
        }
    }


    abstract class InsertBase : Transform
    {
        internal InsertBase()
            : base(TransformFlags.UseParentAsTargetNode, MissingTargetMessage.Error)
        { }

        XmlElement? _siblingElement;

        protected XmlElement SiblingElement
        {
            get
            {
                if (_siblingElement == null)
                {
                    if (Arguments == null || Arguments.Count == 0)
                        throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_InsertMissingArgument, GetType().Name));

                    if (Arguments.Count > 1)
                        throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_InsertTooManyArguments, GetType().Name));

                    string xpath = Arguments[0];
                    XmlNodeList siblings = TargetNode.SelectNodes(xpath);
                    if (siblings.Count == 0)
                        throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_InsertBadXPath, xpath));

                    _siblingElement = siblings[0] as XmlElement;
                    if (_siblingElement == null)
                        throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_InsertBadXPathResult, xpath));
                }

                return _siblingElement;
            }
        }
    }

    class InsertAfter : InsertBase
    {
        protected override void Apply()
        {
            SiblingElement.ParentNode.InsertAfter(TransformNode, SiblingElement);

            Log.LogMessage(MessageType.Verbose, string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_TransformMessageInsert, TransformNode.Name));
        }
    }

    class InsertBefore : InsertBase
    {
        protected override void Apply()
        {
            SiblingElement.ParentNode.InsertBefore(TransformNode, SiblingElement);

            Log.LogMessage(MessageType.Verbose, string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_TransformMessageInsert, TransformNode.Name));
        }
    }

    class SetAttributes : AttributeTransform
    {
        protected override void Apply()
        {
            foreach (XmlAttribute transformAttribute in TransformAttributes)
            {
                if (TargetNode.Attributes.GetNamedItem(transformAttribute.Name) is XmlAttribute targetAttribute)
                    targetAttribute.Value = transformAttribute.Value;
                else
                    TargetNode.Attributes.Append((XmlAttribute) transformAttribute.CloneNode(true));

                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageSetAttribute, transformAttribute.Name);
            }

            if (TransformAttributes.Count > 0)
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageSetAttributes, TransformAttributes.Count);
            else
                Log.LogWarning(SR.XMLTRANSFORMATION_TransformMessageNoSetAttributes);
        }
    }


    class SetTokenizedAttributeStorage
    {
        public List<Dictionary<string, string>> DictionaryList { get; set; }
        public string TokenFormat { get; set; }
        public bool EnableTokenizeParameters { get; set; }
        public bool UseXpathToFormParameter { get; set; }
        public SetTokenizedAttributeStorage() : this(4) { }
        public SetTokenizedAttributeStorage(int capacity)
        {
            DictionaryList = new List<Dictionary<string, string>>(capacity);
            TokenFormat = string.Concat("$(ReplacableToken_#(", SetTokenizedAttributes.ParameterAttribute, ")_#(", SetTokenizedAttributes.TokenNumber, "))");
            EnableTokenizeParameters = false;
            UseXpathToFormParameter = true;
        }
    }

    /// <summary>
    /// Utility class to Transform the SetAttribute to replace token
    /// 1. if it trigger by the regular TransformXml task, it only replace the $(name) from the parent node
    /// 2. If it trigger by the TokenizedTransformXml task, it replace $(name) then parse the declareation of the parameter
    /// </summary>
    class SetTokenizedAttributes : AttributeTransform
    {
        SetTokenizedAttributeStorage? _storageDictionary;
        bool _fInitStorageDictionary;
        public static readonly string Token = "Token";
        public static readonly string TokenNumber = "TokenNumber";
        public static readonly string XPathWithIndex = "XPathWithIndex";
        public static readonly string ParameterAttribute = "Parameter";
        public static readonly string XpathLocator = "XpathLocator";
        public static readonly string XPathWithLocator = "XPathWithLocator";

        XmlAttribute? _tokenizeValueCurrentXmlAttribute;


        protected SetTokenizedAttributeStorage TransformStorage
        {
            get
            {
                if (_storageDictionary == null && !_fInitStorageDictionary)
                {
                    _storageDictionary = GetService<SetTokenizedAttributeStorage>();
                    _fInitStorageDictionary = true;
                }
                return _storageDictionary!;
            }
        }

        protected override void Apply()
        {
            var fTokenizeParameter = false;
            SetTokenizedAttributeStorage storage = TransformStorage;
            List<Dictionary<string, string>>? parameters = null;

            if (storage != null)
            {
                fTokenizeParameter = storage.EnableTokenizeParameters;
                if (fTokenizeParameter)
                    parameters = storage.DictionaryList;
            }

            foreach (XmlAttribute transformAttribute in TransformAttributes)
            {
                XmlAttribute? targetAttribute = TargetNode.Attributes.GetNamedItem(transformAttribute.Name) as XmlAttribute;

                string newValue = TokenizeValue(targetAttribute, transformAttribute, fTokenizeParameter, parameters);

                if (targetAttribute != null)
                    targetAttribute.Value = newValue;
                else
                {
                    var newAttribute = (XmlAttribute) transformAttribute.CloneNode(true);
                    newAttribute.Value = newValue;
                    TargetNode.Attributes.Append(newAttribute);
                }

                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageSetAttribute, transformAttribute.Name);
            }

            if (TransformAttributes.Count > 0)
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageSetAttributes, TransformAttributes.Count);
            else
                Log.LogWarning(SR.XMLTRANSFORMATION_TransformMessageNoSetAttributes);
        }


        static RegularExpressions.Regex? _sDirRegex;
        static RegularExpressions.Regex? _sParentAttribRegex;
        static RegularExpressions.Regex? _sTokenFormatRegex;

        // Directory registrory
        internal static RegularExpressions.Regex DirRegex 
            => _sDirRegex ??= new RegularExpressions.Regex(@"\G\{%(\s*(?<attrname>\w+(?=\W))(\s*(?<equal>=)\s*'(?<attrval>[^']*)'|\s*(?<equal>=)\s*(?<attrval>[^\s%>]*)|(?<equal>)(?<attrval>\s*?)))*\s*?%\}");

        internal static RegularExpressions.Regex ParentAttributeRegex 
            => _sParentAttribRegex ??= new RegularExpressions.Regex(@"\G\$\((?<tagname>[\w:\.]+)\)");

        internal static RegularExpressions.Regex TokenFormatRegex 
            => _sTokenFormatRegex ??= new RegularExpressions.Regex(@"\G\#\((?<tagname>[\w:\.]+)\)");

        protected delegate string? GetValueCallback(string key);

        protected string? GetAttributeValue(string attributeName)
        {
            string? dataValue = null;
            var sourceAttribute = TargetNode.Attributes.GetNamedItem(attributeName) as XmlAttribute;
            if (sourceAttribute == null)
            {
                // if it is other attributename, we fall back to the current now 
                if (string.Compare(attributeName, _tokenizeValueCurrentXmlAttribute!.Name, StringComparison.OrdinalIgnoreCase) != 0) 
                    sourceAttribute = TransformNode.Attributes.GetNamedItem(attributeName) as XmlAttribute;
            }
            if (sourceAttribute != null)
                dataValue = sourceAttribute.Value;
            return dataValue;
        }

        //DirRegex treat single quote differently
        protected string EscapeDirRegexSpecialCharacter(string value, bool escape) 
            => escape ? value.Replace("'", "&apos;") : value.Replace("&apos;", "'");

        protected static string SubstituteKnownValue(string transformValue, RegularExpressions.Regex patternRegex, string patternPrefix, GetValueCallback getValueDelegate)
        {
            var position = 0;
            var matchsExpr = new List<RegularExpressions.Match>();
            do
            {
                position = transformValue.IndexOf(patternPrefix, position, StringComparison.OrdinalIgnoreCase);
                if (position > -1)
                {
                    RegularExpressions.Match match = patternRegex.Match(transformValue, position);
                    // Add the successful match to collection
                    if (match.Success)
                    {
                        matchsExpr.Add(match);
                        position = match.Index + match.Length;
                    }
                    else
                        position++;
                }
            } while (position > -1);

            var strbuilder = new StringBuilder(transformValue.Length);
            if (matchsExpr.Count > 0)
            {
                strbuilder.Remove(0, strbuilder.Length);
                position = 0;
                foreach (RegularExpressions.Match match in matchsExpr)
                {
                    strbuilder.Append(transformValue.Substring(position, match.Index - position));
                    RegularExpressions.Capture captureTagName = match.Groups["tagname"];
                    string attributeName = captureTagName.Value;

                    string? newValue = getValueDelegate(attributeName);

                    // null indicate that the attribute is not exist
                    strbuilder.Append(newValue ?? match.Value);
                    position = match.Index + match.Length;
                }
                strbuilder.Append(transformValue.Substring(position));

                transformValue = strbuilder.ToString();
            }

            return transformValue;
        }

        string GetXPathToAttribute(XmlAttribute? xmlAttribute) 
            => GetXPathToAttribute(xmlAttribute, null);

        string GetXPathToAttribute(XmlAttribute? xmlAttribute, IList<string>? locators)
        {
            string path = string.Empty;
            if (xmlAttribute != null)
            {
                string? pathToNode = GetXPathToNode(xmlAttribute.OwnerElement);
                if (!string.IsNullOrEmpty(pathToNode))
                {
                    var identifier = new StringBuilder(256);
                    if (!(locators == null || locators.Count == 0))
                    {
                        foreach (string match in locators)
                        {
                            string? val = GetAttributeValue(match);
                            if (!string.IsNullOrEmpty(val))
                            {
                                if (identifier.Length != 0)
                                    identifier.Append(" and ");
                                identifier.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "@{0}='{1}'", match, val));
                            }
                            else
                                throw new XmlTransformationException(string.Format(System.Globalization.CultureInfo.CurrentCulture, SR.XMLTRANSFORMATION_MatchAttributeDoesNotExist, match));
                        }
                    }

                    if (identifier.Length == 0)
                    {
                        for (var i = 0; i < TargetNodes.Count; i++)
                        {
                            if (TargetNodes[i] == xmlAttribute.OwnerElement)
                            {
                                // Xpath is 1 based
                                identifier.Append((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            }
                        }
                    }
                    pathToNode = string.Concat(pathToNode, "[", identifier.ToString(), "]");
                }
                path = string.Concat(pathToNode, "/@", xmlAttribute.Name);
            }
            return path;
        }

        static string? GetXPathToNode(XmlNode xmlNode)
        {
            if (xmlNode == null || xmlNode.NodeType == XmlNodeType.Document)
                return null;
            string? parentPath = GetXPathToNode(xmlNode.ParentNode);
            return string.Concat(parentPath, "/", xmlNode.Name);
        }

        string TokenizeValue(XmlAttribute? targetAttribute,
            XmlAttribute transformAttribute,
            bool fTokenizeParameter,
            List<Dictionary<string, string>>? parameters)
        {
            Debug.Assert(!fTokenizeParameter || parameters != null);

            _tokenizeValueCurrentXmlAttribute = transformAttribute;
            string transformValue = transformAttribute.Value;
            string xpath = GetXPathToAttribute(targetAttribute);

            //subsitute the know value first in the transformAttribute
            transformValue = SubstituteKnownValue(transformValue, ParentAttributeRegex, "$(", key => EscapeDirRegexSpecialCharacter(GetAttributeValue(key)!, true));

            // then use the directive to parse the value. --- if TokenizeParameterize is enable
            if (fTokenizeParameter && parameters != null)
            {
                var strbuilder = new StringBuilder(transformValue.Length);
                var position = 0;
                var matchs = new List<RegularExpressions.Match>();

                do
                {
                    position = transformValue.IndexOf("{%", position, StringComparison.OrdinalIgnoreCase);
                    if (position > -1)
                    {
                        RegularExpressions.Match match = DirRegex.Match(transformValue, position);
                        // Add the successful match to collection
                        if (match.Success)
                        {
                            matchs.Add(match);
                            position = match.Index + match.Length;
                        }
                        else
                            position++;
                    }
                } while (position > -1);

                if (matchs.Count > 0)
                {
                    strbuilder.Remove(0, strbuilder.Length);
                    position = 0;
                    var index = 0;

                    foreach (RegularExpressions.Match match in matchs)
                    {
                        strbuilder.Append(transformValue.Substring(position, match.Index - position));
                        RegularExpressions.CaptureCollection attrnames = match.Groups["attrname"].Captures;
                        if (attrnames != null && attrnames.Count > 0)
                        {
                            RegularExpressions.CaptureCollection attrvalues = match.Groups["attrval"].Captures;
                            var paramDictionary = new Dictionary<string, string>(4, StringComparer.OrdinalIgnoreCase)
                            {
                                [XPathWithIndex] = xpath,
                                [TokenNumber] = index.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            };

                            // Get the key-value pare of the in the tranform form
                            for (var i = 0; i < attrnames.Count; i++)
                            {
                                string name = attrnames[i].Value;
                                string? val = null;
                                if (attrvalues != null && i < attrvalues.Count)
                                    val = EscapeDirRegexSpecialCharacter(attrvalues[i].Value, false);
                                paramDictionary[name] = val!;
                            }

                            //Identify the Token format
                            if (!paramDictionary.TryGetValue(Token, out string? strTokenFormat))
                                strTokenFormat = _storageDictionary!.TokenFormat;

                            if (!string.IsNullOrEmpty(strTokenFormat))
                                paramDictionary[Token] = strTokenFormat;

                            // Second translation of #() -- replace with the existing Parameters
                            int count = paramDictionary.Count;
                            var keys = new string[count];
                            paramDictionary.Keys.CopyTo(keys, 0);
                            for (var i = 0; i < count; i++)
                            {
                                // if token format contain the #(),we replace with the known value such that it is unique identify
                                // for example, intokenizeTransformXml.cs, default token format is
                                // string.Concat("$(ReplacableToken_#(", SetTokenizedAttributes.ParameterAttribute, ")_#(", SetTokenizedAttributes.TokenNumber, "))");
                                // which ParameterAttribute will be translate to parameterDictionary["parameter"} and TokenNumber will be translate to parameter 
                                // parameterDictionary["TokenNumber"]
                                string keyindex = keys[i];
                                string val = paramDictionary[keyindex];
                                string newVal = SubstituteKnownValue(val, TokenFormatRegex, "#(",
                                    key => paramDictionary.ContainsKey(key) ? paramDictionary[key] : null);

                                paramDictionary[keyindex] = newVal;
                            }

                            if (paramDictionary.TryGetValue(Token, out strTokenFormat))
                                // Replace with token
                                strbuilder.Append(strTokenFormat);

                            if (paramDictionary.TryGetValue(XpathLocator, out string attributeLocator) && !string.IsNullOrEmpty(attributeLocator))
                            {
                                IList<string> locators = XmlArgumentUtility.SplitArguments(attributeLocator);
                                string xpathwithlocator = GetXPathToAttribute(targetAttribute, locators);

                                if (!string.IsNullOrEmpty(xpathwithlocator))
                                    paramDictionary[XPathWithLocator] = xpathwithlocator;
                            }
                            parameters.Add(paramDictionary);
                        }

                        position = match.Index + match.Length;
                        index++;
                    }
                    strbuilder.Append(transformValue.Substring(position));
                    transformValue = strbuilder.ToString();
                }
            }
            return transformValue;
        }

    }

    class RemoveAttributes : AttributeTransform
    {
        protected override void Apply()
        {
            foreach (XmlAttribute attribute in TargetAttributes)
            {
                TargetNode.Attributes.Remove(attribute);

                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageRemoveAttribute, attribute.Name);
            }

            if (TargetAttributes.Count > 0)
                Log.LogMessage(MessageType.Verbose, SR.XMLTRANSFORMATION_TransformMessageRemoveAttributes, TargetAttributes.Count);
            else
                Log.LogWarning(TargetNode, SR.XMLTRANSFORMATION_TransformMessageNoRemoveAttributes);
        }
    }
}
