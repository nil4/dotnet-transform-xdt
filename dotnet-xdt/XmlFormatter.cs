using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Globalization;

namespace DotNet.Xdt
{
    interface IXmlFormattableAttributes
    {
        void FormatAttributes(XmlFormatter formatter);

        string? AttributeIndent { get; }
    }

    class XmlFormatter
    {
        readonly XmlFileInfoDocument _document;

        readonly LinkedList<string> _indents = new LinkedList<string>();
        readonly LinkedList<string> _attributeIndents = new LinkedList<string>();
        string? _currentIndent = string.Empty;
        string? _currentAttributeIndent;
        string? _oneTab;
        XmlNode? _currentNode;


        public static void Format(XmlDocument document)
        {
            if (document is XmlFileInfoDocument errorInfoDocument)
            {
                var formatter = new XmlFormatter(errorInfoDocument);
                formatter.FormatLoop(errorInfoDocument);
            }
        }

        XmlFormatter(XmlFileInfoDocument document) 
            => _document = document;

        XmlNode? CurrentNode
        {
            get => _currentNode;
            set
            {
                PreviousNode = _currentNode;
                _currentNode = value;
            }
        }

        XmlNode? PreviousNode { get; set; }

        string PreviousIndent
        {
            get
            {
                Debug.Assert(_indents.Count > 0, "Expected at least one previous indent");
                return _indents.Last.Value;
            }
        }

        string CurrentIndent => _currentIndent ??= ComputeCurrentIndent();

        public string? CurrentAttributeIndent => _currentAttributeIndent ??= ComputeCurrentAttributeIndent();

        string OneTab => _oneTab ??= ComputeOneTab();

        const string DefaultTab = "\t";

        void FormatLoop(XmlNode parentNode)
        {
            for (var i = 0; i < parentNode.ChildNodes.Count; i++)
            {
                XmlNode node = parentNode.ChildNodes[i];
                CurrentNode = node;

                switch (node.NodeType)
                {
                case XmlNodeType.Element:
                    i += HandleElement(node);
                    break;
                case XmlNodeType.Whitespace:
                    i += HandleWhiteSpace(node);
                    break;
                case XmlNodeType.Comment:
                case XmlNodeType.Entity:
                    i += EnsureNodeIndent(node, false);
                    break;
                case XmlNodeType.ProcessingInstruction:
                case XmlNodeType.CDATA:
                case XmlNodeType.Text:
                case XmlNodeType.EntityReference:
                case XmlNodeType.DocumentType:
                case XmlNodeType.XmlDeclaration:
                    // Do nothing
                    break;
                default:
                    Debug.Fail(string.Format(CultureInfo.InvariantCulture, "Unexpected element type '{0}' while formatting document", node.NodeType.ToString()));
                    break;
                }
            }
        }

        void FormatAttributes(XmlNode node) => (node as IXmlFormattableAttributes)?.FormatAttributes(this);

        int HandleElement(XmlNode node)
        {
            int indexChange = HandleStartElement(node);

            ReorderNewItemsAtEnd(node);

            // Loop over children
            FormatLoop(node);

            CurrentNode = node;

            indexChange += HandleEndElement(node);

            return indexChange;
        }

        // This is a special case for preserving the whitespace that existed
        // before the end tag of this node. If elements were inserted after
        // that whitespace, the whitespace needs to be moved back to the
        // end of the child list, and new whitespaces should be inserted
        // *before* the new nodes.
        void ReorderNewItemsAtEnd(XmlNode node)
        {
            // If this is a new node, then there couldn't be original
            // whitespace before the end tag
            if (!IsNewNode(node))
            {

                // If the last child isn't whitespace, new elements might
                // have been added
                XmlNode iter = node.LastChild;
                if (iter != null && iter.NodeType != XmlNodeType.Whitespace)
                {

                    // The loop continues until we find something that isn't
                    // a new Element. If it's whitespace, then that will be
                    // the whitespace we need to move.
                    XmlNode? whitespace = null;
                    while (iter != null)
                    {
                        switch (iter.NodeType)
                        {
                        case XmlNodeType.Whitespace:
                            // Found the whitespace, loop can stop
                            whitespace = iter;
                            break;
                        case XmlNodeType.Element:
                            // Loop continues over new Elements
                            if (IsNewNode(iter))
                            {
                                iter = iter.PreviousSibling;
                                continue;
                            }
                            break;
                        default:
                            // Anything else stops the loop
                            break;
                        }
                        break;
                    }

                    if (whitespace != null)
                    {
                        // We found whitespace to move. Remove it from where
                        // it is and add it back to the end
                        node.RemoveChild(whitespace);
                        node.AppendChild(whitespace);
                    }
                }
            }
        }

        int HandleStartElement(XmlNode node)
        {
            int indexChange = EnsureNodeIndent(node, false);

            FormatAttributes(node);

            PushIndent();

            return indexChange;
        }

        int HandleEndElement(XmlNode node)
        {
            var indexChange = 0;

            PopIndent();

            if (!((XmlElement) node).IsEmpty)
                indexChange = EnsureNodeIndent(node, true);

            return indexChange;
        }

        int HandleWhiteSpace(XmlNode node)
        {
            var indexChange = 0;

            // If we find two WhiteSpace nodes in a row, it means some node
            // was removed in between. Remove the previous whitespace.
            // Dev10 bug 830497 Need to improve Error messages from the publish pipeline
            // basically the PreviousNode can be null, it need to be check before use the note.
            if (IsWhiteSpace(PreviousNode))
            {
                // Prefer to keep 'node', but if 'PreviousNode' has a newline
                // and 'node' doesn't, keep the whitespace with the newline
                XmlNode? removeNode = PreviousNode;
                if (FindLastNewLine(node.OuterXml) < 0 && FindLastNewLine(PreviousNode!.OuterXml) >= 0)
                    removeNode = node;

                removeNode?.ParentNode?.RemoveChild(removeNode);
                indexChange = -1;
            }

            string? indent = GetIndentFromWhiteSpace(node);
            if (indent != null)
                SetIndent(indent);

            return indexChange;
        }

        int EnsureNodeIndent(XmlNode node, bool indentBeforeEnd)
        {
            var indexChange = 0;

            if (NeedsIndent(node, PreviousNode))
            {
                if (indentBeforeEnd)
                    InsertIndentBeforeEnd(node);
                else
                {
                    InsertIndentBefore(node);
                    indexChange = 1;
                }
            }

            return indexChange;
        }

        string? GetIndentFromWhiteSpace(XmlNode node)
        {
            string whitespace = node.OuterXml;
            int index = FindLastNewLine(whitespace);
            // If there's no newline, then this is whitespace in the
            // middle of a line, not an indent
            return index >= 0 ? whitespace.Substring(index) : null;
        }

        static int FindLastNewLine(string whitespace)
        {
            for (int i = whitespace.Length - 1; i >= 0; i--)
            {
                switch (whitespace[i])
                {
                case '\r':
                    return i;
                case '\n':
                    if (i > 0 && whitespace[i - 1] == '\r')
                        return i - 1;
                    return i;
                case ' ':
                case '\t':
                    break;
                default:
                    // Non-whitespace character, not legal in indent text
                    return -1;
                }
            }

            // No newline found
            return -1;
        }

        void SetIndent(string indent)
        {
            if (_currentIndent == null || !_currentIndent.Equals(indent))
            {
                _currentIndent = indent;

                // These strings will be determined on demand
                _oneTab = null;
                _currentAttributeIndent = null;
            }
        }

        void PushIndent()
        {
            _indents.AddLast(new LinkedListNode<string>(CurrentIndent));

            // The next indent will be determined on demand, assuming
            // we don't find one before it's needed
            _currentIndent = null;

            // Don't use the property accessor to push the attribute
            // indent. These aren't always needed, so we don't compute
            // them until necessary. Also, we don't walk through this
            // stack like we do the indents stack.
            _attributeIndents.AddLast(new LinkedListNode<string>(_currentAttributeIndent!));
            _currentAttributeIndent = null;
        }

        void PopIndent()
        {
            if (_indents.Count > 0)
            {
                _currentIndent = _indents.Last.Value;
                _indents.RemoveLast();

                _currentAttributeIndent = _attributeIndents.Last.Value;
                _attributeIndents.RemoveLast();
            }
            else
                throw new InvalidOperationException("Popped too many indents");
        }

        bool NeedsIndent(XmlNode node, XmlNode? previousNode)
        {
            return !IsWhiteSpace(previousNode)
                && !IsText(previousNode)
                && (IsNewNode(node) || IsNewNode(previousNode));
        }

        static bool IsWhiteSpace(XmlNode? node) => node != null && node.NodeType == XmlNodeType.Whitespace;

        public bool IsText(XmlNode? node) => node != null && node.NodeType == XmlNodeType.Text;

        bool IsNewNode(XmlNode? node) => node != null && _document.IsNewNode(node);

        void InsertIndentBefore(XmlNode node) => node.ParentNode.InsertBefore(_document.CreateWhitespace(CurrentIndent), node);

        void InsertIndentBeforeEnd(XmlNode node) => node.AppendChild(_document.CreateWhitespace(CurrentIndent));

        string ComputeCurrentIndent() => LookAheadForIndent() ?? PreviousIndent + OneTab;

        string? LookAheadForIndent()
        {
            if (_currentNode?.ParentNode == null)
                return null;

            foreach (XmlNode siblingNode in _currentNode.ParentNode.ChildNodes)
            {
                if (IsWhiteSpace(siblingNode) && siblingNode.NextSibling != null)
                {
                    string whitespace = siblingNode.OuterXml;
                    int index = FindLastNewLine(whitespace);
                    if (index >= 0)
                        return whitespace.Substring(index);
                }
            }

            return null;
        }

        string ComputeOneTab()
        {
            Debug.Assert(_indents.Count > 0, "Expected at least one previous indent");
            if (_indents.Count < 0)
                return DefaultTab;

            LinkedListNode<string> currentIndentNode = _indents.Last;
            LinkedListNode<string> previousIndentNode = currentIndentNode.Previous;

            while (previousIndentNode != null)
            {
                // If we can determine the difference between the current indent
                // and the previous one, then that's the value of one tab
                if (currentIndentNode.Value.StartsWith(previousIndentNode.Value, StringComparison.Ordinal))
                    return currentIndentNode.Value.Substring(previousIndentNode.Value.Length);

                currentIndentNode = previousIndentNode;
                previousIndentNode = currentIndentNode.Previous;
            }

            return ConvertIndentToTab(currentIndentNode.Value);
        }

        static string ConvertIndentToTab(string indent)
        {
            for (var index = 0; index < indent.Length - 1; index++)
            {
                switch (indent[index])
                {
                case '\r':
                case '\n':
                    break;
                default:
                    return indent.Substring(index + 1);
                }
            }

            // There were no characters after the newlines, or the string was
            // empty. Fall back on the default value
            return DefaultTab;
        }

        string? ComputeCurrentAttributeIndent()
        {
            string? siblingIndent = LookForSiblingIndent(CurrentNode);
            if (siblingIndent != null)
                return siblingIndent;
            return CurrentIndent + OneTab;
        }

        static string? LookForSiblingIndent(XmlNode? currentNode)
        {
            var beforeCurrentNode = true;
            string? foundIndent = null;

            foreach (XmlNode node in currentNode?.ParentNode?.ChildNodes!)
            {
                if (node == currentNode)
                    beforeCurrentNode = false;
                else
                {
                    if (node is IXmlFormattableAttributes xfa)
                        foundIndent = xfa.AttributeIndent;
                }

                if (!beforeCurrentNode && foundIndent != null)
                    return foundIndent;
            }

            return null;
        }
    }
}
