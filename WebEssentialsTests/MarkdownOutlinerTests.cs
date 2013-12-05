﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using MadsKristensen.EditorExtensions.Classifications.Markdown;
using Microsoft.Html.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace WebEssentialsTests
{
    [TestClass]
    public class MarkdownOutlinerTests
    {
        static void TestOutlines(string markdown, params Tuple<string, string[]>[] expectedOutlines)
        {

            var snapshot = new MockSnapshot(markdown.Replace("{[", "").Replace("]}", ""));

            var expected = new List<TagSpan<IOutliningRegionTag>>();
            var lastIndex = 0;
            for (int i = 0; i < expectedOutlines.Length; i++)
            {
                var spanStart = markdown.IndexOf("{[", lastIndex);
                if (spanStart < 0)
                    throw new ArgumentException("Not enough test delimiters");

                markdown = markdown.Remove(spanStart, 2);
                var spanEnd = markdown.IndexOf("]}", spanStart);
                markdown = markdown.Remove(spanEnd, 2);
                expected.Add(new TagSpan<IOutliningRegionTag>(
                    new SnapshotSpan(snapshot, Span.FromBounds(spanStart, spanEnd)),
                    new SimpleOutlineTag(expectedOutlines[i])
                ));
                lastIndex = spanEnd;
            }
            if (markdown != snapshot.GetText())
                throw new ArgumentException("Unexpected test delimiters");

            var artifacts = new ArtifactCollection(new MarkdownCodeArtifactProcessor());
            artifacts.Build(markdown);

            var tagger = new MarkdownOutlineTagger(artifacts, (c, t) => new SimpleOutlineTag(c, t));
            var actual = tagger.GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(
                snapshot,
                new Span(0, markdown.Length)
            )));
            actual
                .Select(ts => new { ts.Span.Span, ts.Tag })
                .ShouldAllBeEquivalentTo(expected.Select(ts => new { ts.Span.Span, ts.Tag }));
        }

        class SimpleOutlineTag : IOutliningRegionTag
        {
            public SimpleOutlineTag(Tuple<string, string[]> tuple)
            {
                CollapsedText = tuple.Item1;
                HintLines = tuple.Item2;
            }

            public SimpleOutlineTag(object collapsed, IEnumerable<SnapshotSpan> lines)
            {
                CollapsedText = collapsed.ToString();
                HintLines = lines.Select(s => s.GetText()).ToList();
            }
            public string CollapsedText { get; private set; }
            public IEnumerable<string> HintLines { get; private set; }
            object IOutliningRegionTag.CollapsedForm { get { return CollapsedText; } }
            object IOutliningRegionTag.CollapsedHintForm { get { return HintLines; } }
            bool IOutliningRegionTag.IsDefaultCollapsed { get { return false; } }
            bool IOutliningRegionTag.IsImplementation { get { return true; } }

            public override bool Equals(object obj)
            {
                var tag = obj as SimpleOutlineTag;
                return tag != null
                    && EqualityComparer<string>.Default.Equals(CollapsedText, tag.CollapsedText)
                    && EqualityComparer<string>.Default.Equals(String.Join(Environment.NewLine, HintLines), String.Join(Environment.NewLine, tag.HintLines));
            }

            public override int GetHashCode()
            {
                var hashCode = EqualityComparer<string>.Default.GetHashCode(CollapsedText);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(String.Join(Environment.NewLine, HintLines));
                return hashCode;
            }
        }

        [TestMethod]
        public void TestCodeBlocks()
        {
            TestOutlines(@"`code`");

            TestOutlines(@"`code` `more`
`even more inline code blocks should not be outlined`");

            TestOutlines(@"
    Single indented line should not be outlines");

            TestOutlines(@"{[```
First line
Second line
```]}", Tuple.Create("[ Code Block ]", new[] { "First line", "Second line" }));

            TestOutlines(@"
{[    First line
    Second line]}
{[```html
First line2
Second line2
```]}",
    Tuple.Create("[ Code Block ]", new[] { "First line", "Second line" }),
    Tuple.Create("[ html Code Block ]", new[] { "First line2", "Second line2" })
);
        }
        // TODO: Test quoted code blocks, test overlapping partial spans.
    }
}
