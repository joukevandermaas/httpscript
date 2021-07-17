using HttpScript.Parsing;
using HttpScript.Parsing.Tokens;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static HttpScript.Parsing.Tokens.TokenType;
using static Tests.TokenAsserts;

namespace Tests
{
    public class LexerBreakoutTests
    {
        [Theory]
        [InlineData(WhiteSpace, " \t\n\r\n\r \n")]
        [InlineData(Comment, "/* multiline\n\ncomment */")]
        [InlineData(Comment, "/* nested /*multiline\n*/\ncomment */")]
        [InlineData(Comment, "//single-line comment\n")]
        [InlineData(Symbol, "_symbol", "_symbol")]
        [InlineData(Symbol, "$symbol", "$symbol")]
        [InlineData(Symbol, "symbol", "symbol")]
        [InlineData(Symbol, "_SYMBOL", "_SYMBOL")]
        [InlineData(Symbol, "$SYMBOL", "$SYMBOL")]
        [InlineData(Symbol, "SYMBOL", "SYMBOL")]
        [InlineData(String, "'✨'", "✨")]
        [InlineData(String, "\"✨\"", "✨")]
        [InlineData(Operator, "=", OperatorType.Assignment)]
        [InlineData(Operator, ".", OperatorType.MemberAccess)]
        [InlineData(Operator, ",", OperatorType.Separator)]
        public void RecognizesSingleToken(TokenType tokenType, string program, object content = null)
        {
            var tokens = RunLexer(program);

            Assert.Collection(tokens, (t) => Assert.Equal(tokenType, t.Type));

            var token = tokens.Single();

            // the token should have the correct type
            Assert.Equal(tokenType, token.Type);

            // it should have consumed the whole input
            Assert.Equal(0, token.Range.StartOffset);
            Assert.Equal(program.Length, token.Range.EndOffset);

            AssertContent(content, token);
        }

        [Theory]
        [InlineData("(", ParenType.Round, ParenMode.Open)]
        [InlineData(")", ParenType.Round, ParenMode.Close)]
        [InlineData("[", ParenType.Square, ParenMode.Open)]
        [InlineData("]", ParenType.Square, ParenMode.Close)]
        [InlineData("{", ParenType.Curly, ParenMode.Open)]
        [InlineData("}", ParenType.Curly, ParenMode.Close)]
        public void RecognizesSingleParen(string program, ParenType parenType, ParenMode parenMode)
        {
            RecognizesSingleToken(Paren, program, (parenType, parenMode));
        }

        [Theory]
        [InlineData(Comment, ErrorType.MissingEndComment, "/* comment")]
        [InlineData(Comment, ErrorType.MissingEndComment, "/* /* /* comment */ */")]
        [InlineData(String, ErrorType.MissingEndQuote, "\"✨", "✨")]
        [InlineData(String, ErrorType.MissingEndQuote, "\'✨", "✨")]
        [InlineData(String, ErrorType.MissingEndQuote, "\"✨\n", "✨")]
        [InlineData(String, ErrorType.MissingEndQuote, "\'✨\n", "✨")]
        public void RecoversFromPredictableErrors(
            TokenType tokenType,
            ErrorType errorType,
            string program,
            object content = null)
        {
            var tokens = RunLexer(program);

            var asserts = new List<System.Action<Token>>()
            {
                (t) => Assert.Equal(errorType, (t as ErrorToken).ErrorCode),
                (t) => Assert.Equal(tokenType, t.Type),
            };

            if (program.EndsWith('\n'))
            {
                asserts.Add((t) => Assert.Equal(WhiteSpace, t.Type));
            }

            Assert.Collection(tokens, asserts.ToArray());

            var token = tokens.Skip(1).First();

            AssertContent(content, token);
        }

        [Fact]
        public void ParsesBasicProgram()
        {
            const string program = @"
// assign the thing to the thing
$myVal = symbol.method($something, test.var, 'some string');
";

            var tokens = RunLexer(program);

            var asserts = new List<System.Action<Token>>()
            {
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Comment, t),
                (t) => AssertToken(Symbol, "$myVal", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Operator, OperatorType.Assignment, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "symbol", t),
                (t) => AssertToken(Operator, OperatorType.MemberAccess, t),
                (t) => AssertToken(Symbol, "method", t),
                (t) => AssertToken(Paren, (ParenType.Round, ParenMode.Open), t),
                (t) => AssertToken(Symbol, "$something", t),
                (t) => AssertToken(Operator, OperatorType.Separator, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "test", t),
                (t) => AssertToken(Operator, OperatorType.MemberAccess, t),
                (t) => AssertToken(Symbol, "var", t),
                (t) => AssertToken(Operator, OperatorType.Separator, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(String, "some string", t),
                (t) => AssertToken(Paren, (ParenType.Round, ParenMode.Close), t),
                (t) => AssertToken(Operator, OperatorType.EndStatement, t),
                (t) => AssertToken(WhiteSpace, t),
            };

            Assert.Collection(tokens, asserts.ToArray());
        }

        [Fact]
        public void ParsesBasicProgramWithErrors()
        {
            const string program = @"
// assign the thing to the thing
$something = ""unfinished string
+
$myVal = symbol.method($something, test.var, 'some string');
";

            var tokens = RunLexer(program);

            Assert.Collection(tokens,
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Comment, t),
                (t) => AssertToken(Symbol, "$something", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Operator, OperatorType.Assignment, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Error, ErrorType.MissingEndQuote, t),
                (t) => AssertToken(String, "unfinished string", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Error, ErrorType.UnknownToken, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "$myVal", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Operator, OperatorType.Assignment, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "symbol", t),
                (t) => AssertToken(Operator, OperatorType.MemberAccess, t),
                (t) => AssertToken(Symbol, "method", t),
                (t) => AssertToken(Paren, (ParenType.Round, ParenMode.Open), t),
                (t) => AssertToken(Symbol, "$something", t),
                (t) => AssertToken(Operator, OperatorType.Separator, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "test", t),
                (t) => AssertToken(Operator, OperatorType.MemberAccess, t),
                (t) => AssertToken(Symbol, "var", t),
                (t) => AssertToken(Operator, OperatorType.Separator, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(String, "some string", t),
                (t) => AssertToken(Paren, (ParenType.Round, ParenMode.Close), t),
                (t) => AssertToken(Operator, OperatorType.EndStatement, t),
                (t) => AssertToken(WhiteSpace, t)
            );
        }


        private static List<Token> RunLexer(string script) =>
            new Lexer(script) { ParsingMode = ParsingMode.Breakout }.GetTokens().ToList();
    }
}
