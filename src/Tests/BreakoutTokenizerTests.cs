using HttpScript.Parsing;
using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static HttpScript.Parsing.Tokens.TokenType;
using static Tests.TokenAsserts;

namespace Tests
{
    public class BreakoutTokenizerTests
    {
        [Theory]
        [InlineData(WhiteSpace, " \t\n\r\n\r \n")]
        [InlineData(Comment, "/* multiline\n\ncomment */")]
        [InlineData(Comment, "/* nested /*multiline\n*/\ncomment */")]
        [InlineData(Comment, "//single-line comment\n")]
        [InlineData(Symbol, "_symbol", "_symbol")]
        [InlineData(Symbol, "symbol12", "symbol12")]
        [InlineData(Symbol, "symbol", "symbol")]
        [InlineData(Symbol, "_SYMBOL", "_SYMBOL")]
        [InlineData(Symbol, "SYMBOL12", "SYMBOL12")]
        [InlineData(Symbol, "SYMBOL", "SYMBOL")]
        [InlineData(StringLiteral, "'✨'", "✨")]
        [InlineData(StringLiteral, "\"✨\"", "✨")]
        [InlineData(BinaryOperator, "=", '=')]
        [InlineData(BinaryOperator, ".", '.')]
        [InlineData(Separator, ",", ',')]
        [InlineData(Separator, ";", ';')]
        [InlineData(NumberLiteral, "10", 10)]
        [InlineData(NumberLiteral, "100", 100)]
        [InlineData(NumberLiteral, "39843", 39843)]
        [InlineData(NumberLiteral, "000000", 0)]
        [InlineData(Paren, "(", '(')]
        [InlineData(Paren, ")", ")")] // vs test runner breaks if the closing paren content is a char
        [InlineData(Paren, "[", '[')]
        [InlineData(Paren, "]", ']')]
        [InlineData(Paren, "{", '{')]
        [InlineData(Paren, "}", '}')]
        public void RecognizesSingleToken(TokenType tokenType, string program, object content = null)
        {
            var tokens = RunTokenizer(program);

            Assert.Collection(tokens, (t) => Assert.Equal(tokenType, t.Type));

            var token = tokens.Single();

            // the token should have the correct type
            Assert.Equal(tokenType, token.Type);

            Assert.Equal(program, new string(token.Text.Span));

            // it should have consumed the whole input
            Assert.Equal(0, token.Range.StartOffset);
            Assert.Equal(program.Length, token.Range.EndOffset);

            if (tokenType == Paren && content is string strContent)
            {
                // work around visual studio test runner bug
                content = strContent[0];
            }

            AssertContent(content, token);
        }

        [Theory]
        [InlineData(Comment, ErrorStrings.MissingEndComment, "/* comment")]
        [InlineData(Comment, ErrorStrings.MissingEndComment, "/* /* /* comment */ */")]
        [InlineData(StringLiteral, ErrorStrings.MissingEndQuote, "\"✨", "✨")]
        [InlineData(StringLiteral, ErrorStrings.MissingEndQuote, "\'✨", "✨")]
        [InlineData(StringLiteral, ErrorStrings.MissingEndQuote, "\"✨\n", "✨")]
        [InlineData(StringLiteral, ErrorStrings.MissingEndQuote, "\'✨\n", "✨")]
        [InlineData(WhiteSpace, ErrorStrings.InvalidToken, "✨ ")]
        [InlineData(Unknown, ErrorStrings.InvalidToken, "✨")]
        public void RecoversFromPredictableErrors(
            TokenType tokenType,
            string errorMessage,
            string program,
            object content = null)
        {
            var tokens = RunTokenizer(program);

            var asserts = new List<Action<Token>>()
            {
                (t) => Assert.Equal(errorMessage, t.Value),
            };

            if (tokenType != Unknown)
            {
                asserts.Add((t) => Assert.Equal(tokenType, t.Type));
            }

            if (program.EndsWith('\n'))
            {
                asserts.Add((t) => Assert.Equal(WhiteSpace, t.Type));
            }

            Assert.Collection(tokens, asserts.ToArray());

            if (tokens.Count > 1)
            {
                var token = tokens.Skip(1).First();
                AssertContent(content, token);
            }
        }

        [Fact]
        public void ParsesBasicProgram()
        {
            const string program = @"
// assign the thing to the thing
myVal = symbol.method(something, test.var, 'some string');
";

            var tokens = RunTokenizer(program);

            var asserts = new List<Action<Token>>()
            {
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Comment, t),
                (t) => AssertToken(Symbol, "myVal", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(BinaryOperator, '=', t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "symbol", t),
                (t) => AssertToken(BinaryOperator, '.', t),
                (t) => AssertToken(Symbol, "method", t),
                (t) => AssertToken(Paren, '(', t),
                (t) => AssertToken(Symbol, "something", t),
                (t) => AssertToken(Separator, ',', t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "test", t),
                (t) => AssertToken(BinaryOperator, '.', t),
                (t) => AssertToken(Symbol, "var", t),
                (t) => AssertToken(Separator, ',', t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(StringLiteral, "some string", t),
                (t) => AssertToken(Paren, ')', t),
                (t) => AssertToken(Separator, ';', t),
                (t) => AssertToken(WhiteSpace, t),
            };

            Assert.Collection(tokens, asserts.ToArray());
        }

        [Fact]
        public void ParsesBasicProgramWithErrors()
        {
            const string program = @"
// assign the thing to the thing
something = ""unfinished string
✨
myVal = symbol.method(something, test.var, 'some string');
";

            var tokens = RunTokenizer(program);

            Assert.Collection(tokens,
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Comment, t),
                (t) => AssertToken(Symbol, "something", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(BinaryOperator, '=', t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Error, ErrorStrings.MissingEndQuote, t),
                (t) => AssertToken(StringLiteral, "unfinished string", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Error, ErrorStrings.InvalidToken, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "myVal", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(BinaryOperator, '=', t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "symbol", t),
                (t) => AssertToken(BinaryOperator, '.', t),
                (t) => AssertToken(Symbol, "method", t),
                (t) => AssertToken(Paren, '(', t),
                (t) => AssertToken(Symbol, "something", t),
                (t) => AssertToken(Separator, ',', t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "test", t),
                (t) => AssertToken(BinaryOperator, '.', t),
                (t) => AssertToken(Symbol, "var", t),
                (t) => AssertToken(Separator, ',', t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(StringLiteral, "some string", t),
                (t) => AssertToken(Paren, ')', t),
                (t) => AssertToken(Separator, ';', t),
                (t) => AssertToken(WhiteSpace, t)
            );
        }


        private static List<Token> RunTokenizer(string script) =>
            new Tokenizer(script.AsMemory()) { ParsingMode = ParsingMode.Breakout }.GetTokens().ToList();
    }
}
