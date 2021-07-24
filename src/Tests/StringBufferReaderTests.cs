using HttpScript.Parsing;
using System;
using Xunit;

namespace Tests
{
    public class StringBufferReaderTests
    {
        private static readonly ReadOnlyMemory<char> program = "abcdefghijklmnopqrstuvwxyz".AsMemory();

        [Fact]
        public void CreateSnapshotWorks()
        {
            var reader = new StringBufferReader(program);

            Assert.Equal(default, reader.SnapshotState);

            reader.Advance();
            reader.CreateSnapshot();
            reader.Advance();
            reader.Advance();

            Assert.Equal(1, reader.SnapshotState.CharOffset);
            Assert.Equal(1, reader.SnapshotState.LineNumber);
            Assert.Equal(0, reader.SnapshotState.LineStartOffset);
            Assert.Equal(0, reader.SnapshotState.PreviousLineOffset);
        }

        [Fact]
        public void RestoreSnapshotWorks()
        {
            var reader = new StringBufferReader(program);

            reader.Advance();
            reader.CreateSnapshot();
            reader.Advance();
            reader.Advance();

            reader.RestoreSnapshot();

            Assert.Equal(default, reader.SnapshotState);
            Assert.Equal(1, reader.CurrentState.CharOffset);
            Assert.Equal(1, reader.CurrentState.LineNumber);
            Assert.Equal(0, reader.CurrentState.LineStartOffset);
            Assert.Equal(0, reader.CurrentState.PreviousLineOffset);
        }

        [Fact]
        public void DiscardSnapshotWorks()
        {
            var reader = new StringBufferReader(program);

            reader.Advance();
            reader.CreateSnapshot();
            reader.Advance();
            reader.Advance();

            reader.DiscardSnapshot();

            Assert.Equal(default, reader.SnapshotState);
            Assert.Equal(3, reader.CurrentState.CharOffset);
            Assert.Equal(1, reader.CurrentState.LineNumber);
            Assert.Equal(0, reader.CurrentState.LineStartOffset);
            Assert.Equal(0, reader.CurrentState.PreviousLineOffset);
        }

        [Fact]
        public void GetRangeFromSnapshotWorks()
        {
            var reader = new StringBufferReader(program);

            reader.Advance();
            reader.CreateSnapshot();
            reader.Advance();
            reader.Advance();

            var range = reader.GetRangeFromSnapshot();

            Assert.Equal("L1C2-L1C3", range.ToString());
        }

        [Fact]
        public void TryMatchSequenceAndAdvanceAdvancesIfWholeStringMatched()
        {
            var reader = new StringBufferReader(program);

            var result = reader.TryMatchSequenceAndAdvance("abcd");

            Assert.True(result);
            Assert.Equal("L1C5", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryMatchSequenceAndAdvanceDoesNotAdvanceIfWholeStringNotMatched()
        {
            var reader = new StringBufferReader(program);

            var result = reader.TryMatchSequenceAndAdvance("abce");

            Assert.False(result);
            Assert.Equal("L1C1", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryMatchAndAdvanceAdvancesIfMatched()
        {
            var reader = new StringBufferReader(program);

            var result = reader.TryMatchAndAdvance('a');

            Assert.True(result);
            Assert.Equal("L1C2", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryMatchAndAdvanceDoesNotAdvanceIfNotMatched()
        {
            var reader = new StringBufferReader(program);

            var result = reader.TryMatchAndAdvance('b');

            Assert.False(result);
            Assert.Equal("L1C1", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryMatchAndAdvanceAdvancesIfAnyMatched()
        {
            var reader = new StringBufferReader(program);

            reader.TryMatchAndAdvance(out var _, 'a', 'b');
            var result = reader.TryMatchAndAdvance(out var matched, 'a', 'b');

            Assert.True(result);
            Assert.Equal('b', matched);
            Assert.Equal("L1C3", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryMatchAndAdvanceDoesNotAdvanceIfNoneMatched()
        {
            var reader = new StringBufferReader(program);

            var result = reader.TryMatchAndAdvance(out var _, 'c', 'd');

            Assert.False(result);
            Assert.Equal("L1C1", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryAdvanceAdvancesIfNotAtEnd()
        {
            var reader = new StringBufferReader(program);

            var result = reader.TryAdvance(out var character);

            Assert.True(result);
            Assert.Equal('a', character);
            Assert.Equal("L1C2", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryAdvanceDoesNotAdvanceIfAtEnd()
        {
            var reader = new StringBufferReader("".AsMemory());

            var result = reader.TryAdvance(out var _);

            Assert.False(result);
            Assert.Equal("L1C1", reader.CurrentState.ToString());
        }

        [Fact]
        public void TryPeekDoesNotAdvance()
        {
            var reader = new StringBufferReader(program);

            var result = reader.TryPeek(out var character);

            Assert.True(result);
            Assert.Equal('a', character);
            Assert.Equal("L1C1", reader.CurrentState.ToString());
        }

        [Fact]
        public void PeekFoldsNewlineSequence()
        {
            var reader = new StringBufferReader("\r\n".AsMemory());

            var result = reader.TryPeek(out var character);

            Assert.True(result);
            Assert.Equal('\n', character);
            Assert.Equal("L1C1", reader.CurrentState.ToString());
        }

        [Fact]
        public void AdvanceFoldsNewlineSequence()
        {
            var reader = new StringBufferReader("\r\n".AsMemory());

            var result = reader.TryAdvance(out var character);

            Assert.True(result);
            Assert.Equal('\n', character);
            Assert.Equal("L2C1", reader.CurrentState.ToString());
        }

        [Fact]
        public void GetRangeFromSnapshotWorksWhenEndsOnNewline()
        {
            var reader = new StringBufferReader("abcd\r\nefgh".AsMemory());

            reader.CreateSnapshot();
            reader.Advance(); // a
            reader.Advance(); // b
            reader.Advance(); // c
            reader.Advance(); // d
            reader.Advance(); // \n 

            var range = reader.GetRangeFromSnapshot();

            // range should end on line one and include the
            // newline characters
            Assert.Equal("L1C1-L1C6", range.ToString());
        }
    }
}
