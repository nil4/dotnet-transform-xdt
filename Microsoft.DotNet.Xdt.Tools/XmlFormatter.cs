using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.DotNet.Xdt.Tools
{
    internal interface IXmlFormattableAttributes
    {
        void FormatAttributes(XmlFormatter formatter);

        string AttributeIndent { get; }
    }

    internal class XmlFormatter
    {
        private readonly XmlFileInfoDocument _document;

        private readonly LinkedList<string> _indents = new LinkedList<string>();
        private readonly LinkedList<string> _attributeIndents = new LinkedList<string>();
        private string _currentIndent = string.Empty;
        private string _currentAttributeIndent;
        private string _oneTab;
        private XmlNode _currentNode;


        public static void Format(XmlDocument document)
        {
            var errorInfoDocument = document as XmlFileInfoDocument;
            if (errorInfoDocument != null)
            {
                var formatter = new XmlFormatter(errorInfoDocument);
                formatter.FormatLoop(errorInfoDocument);
            }
        }

        private XmlFormatter(XmlFileInfoDocument document)
        {
            _document = document;
        }

        private XmlNode CurrentNode
        {
            get
            {
                return _currentNode;
            }
            set
            {
                PreviousNode = _currentNode;
                _currentNode = value;
            }
        }

        private XmlNode PreviousNode { get; set; }

        private string PreviousIndent
        {
            get
            {
                Debug.Assert(_indents.Count > 0, "Expected at least one previous indent");
                return _indents.Last.Value;
            }
        }

        private string CurrentIndent => _currentIndent ?? (_currentIndent = ComputeCurrentIndent());

        public string CurrentAttributeIndent => _currentAttributeIndent ?? (_currentAttributeIndent = ComputeCurrentAttributeIndent());

        private string OneTab => _oneTab ?? (_oneTab = ComputeOneTab());

        private const string DefaultTab = "\t";

        private void FormatLoop(XmlNode parentNode)
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

        private void FormatAttributes(XmlNode node) => (node as IXmlFormattableAttributes)?.FormatAttributes(this);

        private int HandleElement(XmlNode node)
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
        private void ReorderNewItemsAtEnd(XmlNode node)
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
                    XmlNode whitespace = null;
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

        private int HandleStartElement(XmlNode node)
        {
            int indexChange = EnsureNodeIndent(node, false);

            FormatAttributes(node);

            PushIndent();

            return indexChange;
        }

        private int HandleEndElement(XmlNode node)
        {
            var indexChange = 0;

            PopIndent();

            if (!((XmlElement)node).IsEmpty)
            {
                indexChange = EnsureNodeIndent(node, true);
            }

            return indexChange;
        }

        private int HandleWhiteSpace(XmlNode node)
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
                XmlNode removeNode = PreviousNode;
                if (FindLastNewLine(node.OuterXml) < 0 &&
                    FindLastNewLine(PreviousNode.OuterXml) >= 0)
                {
                    removeNode = node;
                }

                removeNode.ParentNode.RemoveChild(removeNode);
                indexChange = -1;
            }

            string indent = GetIndentFromWhiteSpace(node);
            if (indent != null)
            {
                SetIndent(indent);
            }

            return indexChange;
        }

        private int EnsureNodeIndent(XmlNode node, bool indentBeforeEnd)
        {
            var indexChange = 0;

            if (NeedsIndent(node, PreviousNode))
            {
                if (indentBeforeEnd)
                {
                    InsertIndentBeforeEnd(node);
                }
                else
                {
                    InsertIndentBefore(node);
                    indexChange = 1;
                }
            }

            return indexChange;
        }

        private string GetIndentFromWhiteSpace(XmlNode node)
        {
            string whitespace = node.OuterXml;
            int index = FindLastNewLine(whitespace);
            if (index >= 0)
            {
                return whitespace.Substring(index);
            }
            // If there's no newline, then this is whitespace in the
            // middle of a line, not an indent
            return null;
        }

        private static int FindLastNewLine(string whitespace)
        {
            for (int i = whitespace.Length - 1; i >= 0; i--)
            {
                switch (whitespace[i])
                {
                case '\r':
                    return i;
                case '\n':
                    if (i > 0 && whitespace[i - 1] == '\r')
                    {
                        return i - 1;
                    }
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

        private void SetIndent(string indent)
        {
            if (_currentIndent == null || !_currentIndent.Equals(indent))
            {
                _currentIndent = indent;

                // These strings will be determined on demand
                _oneTab = null;
                _currentAttributeIndent = null;
            }
        }

        private void PushIndent()
        {
            _indents.AddLast(new LinkedListNode<string>(CurrentIndent));

            // The next indent will be determined on demand, assuming
            // we don't find one before it's needed
            _currentIndent = null;

            // Don't use the property accessor to push the attribute
            // indent. These aren't always needed, so we don't compute
            // them until necessary. Also, we don't walk through this
            // stack like we do the indents stack.
            _attributeIndents.AddLast(new LinkedListNode<string>(_currentAttributeIndent));
            _currentAttributeIndent = null;
        }

        private void PopIndent()
        {
            if (_indents.Count > 0)
            {
                _currentIndent = _indents.Last.Value;
                _indents.RemoveLast();

                _currentAttributeIndent = _attributeIndents.Last.Value;
                _attributeIndents.RemoveLast();
            }
            else
            {
                Debug.Fail("Popped too many indents");
                throw new InvalidOperationException();
            }
        }

        private bool NeedsIndent(XmlNode node, XmlNode previousNode)
        {
            return !IsWhiteSpace(previousNode)
                && !IsText(previousNode)
                && (IsNewNode(node) || IsNewNode(previousNode));
        }

        private static bool IsWhiteSpace(XmlNode node) => node != null && node.NodeType == XmlNodeType.Whitespace;

        public bool IsText(XmlNode node) => node != null && node.NodeType == XmlNodeType.Text;

        private bool IsNewNode(XmlNode node) => node != null && _document.IsNewNode(node);

        private void InsertIndentBefore(XmlNode node) => node.ParentNode.InsertBefore(_document.CreateWhitespace(CurrentIndent), node);

        private void InsertIndentBeforeEnd(XmlNode node) => node.AppendChild(_document.CreateWhitespace(CurrentIndent));

        private string ComputeCurrentIndent()
        {
            string lookAheadIndent = LookAheadForIndent();
            if (lookAheadIndent != null)
            {
                return lookAheadIndent;
            }
            return PreviousIndent + OneTab;
        }

        private string LookAheadForIndent()
        {
            if (_currentNode.ParentNode == null)
            {
                return null;
            }

            foreach (XmlNode siblingNode in _currentNode.ParentNode.ChildNodes)
            {
                if (IsWhiteSpace(siblingNode) && siblingNode.NextSibling != null)
                {
                    string whitespace = siblingNode.OuterXml;
                    int index = FindLastNewLine(whitespace);
                    if (index >= 0)
                    {
                        return whitespace.Substring(index);
                    }
                }
            }

            return null;
        }

        private string ComputeOneTab()
        {
            Debug.Assert(_indents.Count > 0, "Expected at least one previous indent");
            if (_indents.Count < 0)
            {
                return DefaultTab;
            }

            LinkedListNode<string> currentIndentNode = _indents.Last;
            LinkedListNode<string> previousIndentNode = currentIndentNode.Previous;

            while (previousIndentNode != null)
            {
                // If we can determine the difference between the current indent
                // and the previous one, then that's the value of one tab
                if (currentIndentNode.Value.StartsWith(previousIndentNode.Value, StringComparison.Ordinal))
                {
                    return currentIndentNode.Value.Substring(previousIndentNode.Value.Length);
                }

                currentIndentNode = previousIndentNode;
                previousIndentNode = currentIndentNode.Previous;
            }

            return ConvertIndentToTab(currentIndentNode.Value);
        }

        private string ConvertIndentToTab(string indent)
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

        private string ComputeCurrentAttributeIndent()
        {
            string siblingIndent = LookForSiblingIndent(CurrentNode);
            if (siblingIndent != null)
            {
                return siblingIndent;
            }
            return CurrentIndent + OneTab;
        }

        private static string LookForSiblingIndent(XmlNode currentNode)
        {
            var beforeCurrentNode = true;
            string foundIndent = null;

            foreach (XmlNode node in currentNode.ParentNode.ChildNodes)
            {
                if (node == currentNode)
                {
                    beforeCurrentNode = false;
                }
                else
                {
                    IXmlFormattableAttributes formattable = node as IXmlFormattableAttributes;
                    if (formattable != null)
                    {
                        foundIndent = formattable.AttributeIndent;
                    }
                }

                if (!beforeCurrentNode && foundIndent != null)
                {
                    return foundIndent;
                }
            }

            return null;
        }
    }
}
