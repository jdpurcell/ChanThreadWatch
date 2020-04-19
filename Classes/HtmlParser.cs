// --------------------------------------------------------------------------------
// Copyright (c) J.D. Purcell
//
// Licensed under the MIT License (see LICENSE.txt)
// --------------------------------------------------------------------------------
using System;
using System.Linq;
using System.Collections.Generic;

namespace JDP {
	public class HtmlParser {
		private readonly string _preprocessedHtml;
		private readonly List<HtmlTag> _tags;
		private readonly Dictionary<int, int> _offsetToIndex = new Dictionary<int, int>();

		public HtmlParser(string html) {
			_preprocessedHtml = General.NormalizeNewLines(html);
			_tags = new List<HtmlTag>(ParseTags(_preprocessedHtml, 0, _preprocessedHtml.Length));
			for (int i = 0; i < _tags.Count; i++) {
				_offsetToIndex.Add(_tags[i].Offset, i);
			}
		}

		public string PreprocessedHtml => _preprocessedHtml;

		public IList<HtmlTag> Tags => _tags.AsReadOnly();

		public string GetInnerHtml(HtmlTag startTag, HtmlTag endTag) {
			return startTag.IsSelfClosing ? "" : GetSection(_preprocessedHtml, startTag.EndOffset, endTag.Offset);
		}

		public string GetInnerHtml(HtmlTagRange tagRange) {
			return GetInnerHtml(tagRange.StartTag, tagRange.EndTag);
		}

		public IEnumerable<HtmlTag> EnumerateTags(HtmlTag startAfterTag, HtmlTag stopBeforeTag) {
			int startIndex = startAfterTag != null ? (GetTagIndex(startAfterTag) + 1) : 0;
			int stopIndex = stopBeforeTag != null ? (GetTagIndex(stopBeforeTag) - 1) : (_tags.Count - 1);
			for (int i = startIndex; i <= stopIndex; i++) {
				yield return _tags[i];
			}
		}

		public IEnumerable<HtmlTag> EnumerateTags(HtmlTagRange containingTagRange) {
			return EnumerateTags(containingTagRange.StartTag, containingTagRange.EndTag);
		}

		public IEnumerable<HtmlTag> FindTags(bool isEndTag, HtmlTag startAfterTag, HtmlTag stopBeforeTag, params string[] names) {
			return EnumerateTags(startAfterTag, stopBeforeTag)
				.Where(tag => tag.IsEnd == isEndTag && tag.NameEqualsAny(names));
		}

		public IEnumerable<HtmlTag> FindTags(bool isEndTag, HtmlTagRange containingTagRange, params string[] names) {
			return FindTags(isEndTag, containingTagRange.StartTag, containingTagRange.EndTag, names);
		}

		public HtmlTag FindTag(bool isEndTag, HtmlTag startAfterTag, HtmlTag stopBeforeTag, params string[] names) {
			foreach (HtmlTag tag in FindTags(isEndTag, startAfterTag, stopBeforeTag, names)) {
				return tag;
			}
			return null;
		}

		public HtmlTag FindTag(bool isEndTag, HtmlTagRange containingTagRange, params string[] names) {
			return FindTag(isEndTag, containingTagRange.StartTag, containingTagRange.EndTag, names);
		}

		public IEnumerable<HtmlTag> FindStartTags(HtmlTag startAfterTag, HtmlTag stopBeforeTag, params string[] names) {
			return FindTags(false, startAfterTag, stopBeforeTag, names);
		}

		public IEnumerable<HtmlTag> FindStartTags(HtmlTagRange containingTagRange, params string[] names) {
			return FindStartTags(containingTagRange.StartTag, containingTagRange.EndTag, names);
		}

		public IEnumerable<HtmlTag> FindStartTags(params string[] names) {
			return FindTags(false, null, null, names);
		}

		public HtmlTag FindStartTag(HtmlTag startAfterTag, HtmlTag stopBeforeTag, params string[] names) {
			return FindTag(false, startAfterTag, stopBeforeTag, names);
		}

		public HtmlTag FindStartTag(HtmlTagRange containingTagRange, params string[] names) {
			return FindStartTag(containingTagRange.StartTag, containingTagRange.EndTag, names);
		}

		public HtmlTag FindStartTag(params string[] names) {
			return FindTag(false, null, null, names);
		}

		public IEnumerable<HtmlTag> FindEndTags(HtmlTag startAfterTag, HtmlTag stopBeforeTag, params string[] names) {
			return FindTags(true, startAfterTag, stopBeforeTag, names);
		}

		public IEnumerable<HtmlTag> FindEndTags(HtmlTagRange containingTagRange, params string[] names) {
			return FindEndTags(containingTagRange.StartTag, containingTagRange.EndTag, names);
		}

		public IEnumerable<HtmlTag> FindEndTags(params string[] names) {
			return FindTags(true, null, null, names);
		}

		public HtmlTag FindEndTag(HtmlTag startAfterTag, HtmlTag stopBeforeTag, params string[] names) {
			return FindTag(true, startAfterTag, stopBeforeTag, names);
		}

		public HtmlTag FindEndTag(HtmlTagRange containingTagRange, params string[] names) {
			return FindEndTag(containingTagRange.StartTag, containingTagRange.EndTag, names);
		}

		public HtmlTag FindEndTag(params string[] names) {
			return FindTag(true, null, null, names);
		}

		public HtmlTag FindCorrespondingEndTag(HtmlTag tag) {
			return FindCorrespondingEndTag(tag, null);
		}

		public HtmlTag FindCorrespondingEndTag(HtmlTag tag, HtmlTag stopBeforeTag) {
			if (tag == null) {
				return null;
			}
			if (tag.IsEnd) {
				throw new ArgumentException("Tag must be a start tag.");
			}
			if (tag.IsSelfClosing) {
				return tag;
			}
			int startIndex = GetTagIndex(tag) + 1;
			int stopIndex = stopBeforeTag != null ? (GetTagIndex(stopBeforeTag) - 1) : (_tags.Count - 1);
			int depth = 1;
			for (int i = startIndex; i <= stopIndex; i++) {
				HtmlTag tag2 = _tags[i];
				if (!tag2.IsSelfClosing && tag2.NameEquals(tag.Name)) {
					depth += tag2.IsEnd ? -1 : 1;
					if (depth == 0) {
						return tag2;
					}
				}
			}
			return null;
		}

		public HtmlTagRange CreateTagRange(HtmlTag tag) {
			return CreateTagRange(tag, null);
		}

		public HtmlTagRange CreateTagRange(HtmlTag tag, HtmlTag stopBeforeTag) {
			HtmlTag endTag = FindCorrespondingEndTag(tag, stopBeforeTag);
			return (tag != null && endTag != null) ? new HtmlTagRange(tag, endTag) : null;
		}

		private int GetTagIndex(HtmlTag tag) {
			return _offsetToIndex.TryGetValue(tag.Offset, out int i) ? i :
				throw new Exception("Unable to locate the specified tag.");
		}

		private static IEnumerable<HtmlTag> ParseTags(string html, int htmlStart, int htmlEnd) {
			while (htmlStart < htmlEnd) {
				int pos = IndexOf(html, htmlStart, htmlEnd, '<');
				if (pos == -1) yield break;

				HtmlTag tag = new HtmlTag();
				tag.Offset = pos;
				htmlStart = pos + 1;
				tag.IsEnd = StartsWith(html, htmlStart, htmlEnd, '/');
				if (StartsWithLetter(html, tag.IsEnd ? (htmlStart + 1) : htmlStart, htmlEnd)) {
					// Parse tag name
					if (tag.IsEnd) htmlStart += 1;
					pos = IndexOfAny(html, htmlStart, htmlEnd, true, '/', '>');
					if (pos == -1) yield break;
					tag.Name = GetSectionLower(html, htmlStart, pos);
					htmlStart = pos;

					// Parse attributes
					bool isTagComplete = false;
					do {
						while (StartsWithWhiteSpace(html, htmlStart, htmlEnd)) htmlStart++;
						tag.IsSelfClosing = StartsWith(html, htmlStart, htmlEnd, '/');
						if (tag.IsSelfClosing) htmlStart += 1;
						if (StartsWith(html, htmlStart, htmlEnd, '>')) {
							htmlStart += 1;
							isTagComplete = true;
						}
						else if (tag.IsSelfClosing) { }
						else {
							HtmlAttribute attribute = new HtmlAttribute();
							attribute.Offset = htmlStart;

							// Parse attribute name
							pos = IndexOfAny(html, htmlStart + 1, htmlEnd, true, '=', '/', '>');
							if (pos == -1) yield break;
							attribute.Name = GetSectionLower(html, htmlStart, pos);
							htmlStart = pos;

							while (StartsWithWhiteSpace(html, htmlStart, htmlEnd)) htmlStart++;
							if (StartsWith(html, htmlStart, htmlEnd, '=')) {
								// Parse attribute value
								htmlStart += 1;
								while (StartsWithWhiteSpace(html, htmlStart, htmlEnd)) htmlStart++;
								if (StartsWithAny(html, htmlStart, htmlEnd, '"', '\'')) {
									char quoteChar = html[htmlStart];
									htmlStart += 1;
									pos = IndexOf(html, htmlStart, htmlEnd, quoteChar);
									if (pos == -1) yield break;
									attribute.Value = GetSection(html, htmlStart, pos);
									htmlStart = pos + 1;
								}
								else {
									pos = IndexOfAny(html, htmlStart, htmlEnd, true, '>');
									if (pos == -1) yield break;
									attribute.Value = GetSection(html, htmlStart, pos);
									htmlStart = pos;
								}
							}
							else {
								attribute.Value = "";
							}

							attribute.Length = htmlStart - attribute.Offset;
							if (tag.GetAttribute(attribute.Name) == null) {
								tag.Attributes.Add(attribute);
							}
						}
					}
					while (!isTagComplete);
					tag.Length = htmlStart - tag.Offset;

					// Yield result
					yield return tag;

					// Skip contents of special tags whose contents are to be treated as raw text
					if (!tag.IsEnd && !tag.IsSelfClosing && tag.NameEqualsAny("script", "style", "title", "textarea")) {
						bool foundEndTag = false;
						do {
							pos = IndexOf(html, htmlStart, htmlEnd, '<');
							if (pos == -1) yield break;
							htmlStart = pos + 1;
							string endTagText = "/" + tag.Name;
							if (StartsWith(html, htmlStart, htmlEnd, endTagText, true) &&
								(StartsWithWhiteSpace(html, htmlStart + endTagText.Length, htmlEnd) ||
								 StartsWithAny(html, htmlStart + endTagText.Length, htmlEnd, '/', '>')))
							{
								htmlStart -= 1;
								foundEndTag = true;
							}
						}
						while (!foundEndTag);
					}
				}
				else if (StartsWith(html, htmlStart, htmlEnd, "!--", false) && !StartsWith(html, htmlStart + 3, htmlEnd, '>')) {
					// Skip comment
					htmlStart += 3;
					bool foundEnd = false;
					do {
						pos = IndexOf(html, htmlStart, htmlEnd, '-');
						if (pos == -1) yield break;
						htmlStart = pos + 1;
						if (StartsWith(html, htmlStart, htmlEnd, "->", false)) {
							htmlStart += 2;
							foundEnd = true;
						}
						else if (StartsWith(html, htmlStart, htmlEnd, "-!>", false)) {
							htmlStart += 3;
							foundEnd = true;
						}
					}
					while (!foundEnd);
				}
				else if (StartsWithAny(html, htmlStart, htmlEnd, '?', '/', '!')) {
					// Skip bogus comment or DOCTYPE
					htmlStart += 1;
					pos = IndexOf(html, htmlStart, htmlEnd, '>');
					if (pos == -1) yield break;
					htmlStart = pos + 1;
				}
			}
		}

		private static int IndexOf(string html, int htmlStart, int htmlEnd, char value) {
			while (htmlStart < htmlEnd) {
				if (html[htmlStart] == value) {
					return htmlStart;
				}
				htmlStart++;
			}
			return -1;
		}

		private static int IndexOfAny(string html, int htmlStart, int htmlEnd, bool findWhiteSpace, params char[] values) {
			while (htmlStart < htmlEnd) {
				char c = html[htmlStart];
				if (findWhiteSpace && CharIsWhiteSpace(c)) {
					return htmlStart;
				}
				foreach (char v in values) {
					if (c == v) {
						return htmlStart;
					}
				}
				htmlStart++;
			}
			return -1;
		}

		private static bool StartsWith(string html, int htmlStart, int htmlEnd, char value) {
			if (htmlStart >= htmlEnd) return false;
			return html[htmlStart] == value;
		}

		private static bool StartsWith(string html, int htmlStart, int htmlEnd, string value, bool ignoreCase) {
			if (htmlStart + (value.Length - 1) >= htmlEnd) return false;
			for (int i = 0; i < value.Length; i++) {
				char c = html[htmlStart + i];
				char v = value[i];
				if (ignoreCase) {
					c = CharToLower(c);
					v = CharToLower(v);
				}
				if (c != v) return false;
			}
			return true;
		}

		private static bool StartsWithAny(string html, int htmlStart, int htmlEnd, params char[] values) {
			if (htmlStart >= htmlEnd) return false;
			char c = html[htmlStart];
			foreach (char v in values) {
				if (c == v) return true;
			}
			return false;
		}

		private static bool StartsWithWhiteSpace(string html, int htmlStart, int htmlEnd) {
			if (htmlStart >= htmlEnd) return false;
			char c = html[htmlStart];
			return CharIsWhiteSpace(c);
		}

		private static bool StartsWithLetter(string html, int htmlStart, int htmlEnd) {
			if (htmlStart >= htmlEnd) return false;
			return CharIsLetter(html[htmlStart]);
		}

		private static string GetSectionLower(string html, int htmlStart, int htmlEnd) {
			char[] dst = new char[htmlEnd - htmlStart];
			for (int i = 0; i < dst.Length; i++) {
				dst[i] = CharToLower(html[htmlStart + i]);
			}
			return new string(dst);
		}

		private static string GetSection(string html, int htmlStart, int htmlEnd) {
			return html.Substring(htmlStart, htmlEnd - htmlStart);
		}

		private static bool CharIsLetter(char c) {
			return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
		}

		private static bool CharIsWhiteSpace(char c) {
			return c == ' ' || c == '\t' || c == '\f' || c == '\n';
		}

		private static char CharToLower(char c) {
			return (c >= 'A' && c <= 'Z') ? (char)(c + ('a' - 'A')) : c;
		}

		public static char[] GetWhiteSpaceChars() {
			return new char[] { ' ', '\t', '\f', '\n' };
		}

		public static bool ClassAttributeValueHas(string attributeValue, string targetClassName) {
			string[] assignedClassNames = attributeValue.Split(GetWhiteSpaceChars(), StringSplitOptions.RemoveEmptyEntries);
			return Array.Exists(assignedClassNames, n => n.Equals(targetClassName, StringComparison.Ordinal));
		}

		public static bool ClassAttributeValueHas(HtmlTag tag, string targetClassName) {
			string attributeValue = tag.GetAttributeValue("class");
			return attributeValue != null && ClassAttributeValueHas(attributeValue, targetClassName);
		}
	}

	public class HtmlTag {
		public string Name { get; set; }
		public bool IsEnd { get; set; }
		public bool IsSelfClosing { get; set; }
		public List<HtmlAttribute> Attributes { get; set; }
		public int Offset { get; set; }
		public int Length { get; set; }

		public HtmlTag() {
			Attributes  = new List<HtmlAttribute>();
		}

		public int EndOffset {
			get {
				return Offset + Length;
			}
		}

		public bool NameEquals(string name) {
			return Name.Equals(name, StringComparison.OrdinalIgnoreCase);
		}

		public bool NameEqualsAny(params string[] names) {
			foreach (string name in names) {
				if (Name.Equals(name, StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
			return false;
		}

		public HtmlAttribute GetAttribute(string name) {
			foreach (HtmlAttribute attribute in Attributes) {
				if (attribute.NameEquals(name)) {
					return attribute;
				}
			}
			return null;
		}

		public string GetAttributeValue(string attributeName) {
			return GetAttribute(attributeName)?.Value;
		}

		public string GetAttributeValueOrEmpty(string attributeName) {
			return GetAttributeValue(attributeName) ?? "";
		}
	}

	public class HtmlAttribute {
		public string Name { get; set; }
		public string Value { get; set; }
		public int Offset { get; set; }
		public int Length { get; set; }

		public bool NameEquals(string name) {
			return Name.Equals(name, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class HtmlTagRange {
		public HtmlTag StartTag { get; }
		public HtmlTag EndTag { get; }

		public HtmlTagRange(HtmlTag startTag, HtmlTag endTag) {
			StartTag = startTag;
			EndTag = endTag;
		}
	}
}
