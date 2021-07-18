using HttpScript.Parsing;
using HttpScript.Parsing.Tokens;
using System.Collections.Generic;
using Xunit;
using static HttpScript.Parsing.Tokens.TokenType;
using static Tests.TokenAsserts;

namespace Tests
{
    public class LexerTest
    {

        [Fact]
        public void PeekReturnsSameAsConsume()
        {
            const string program = @"
// assign the thing to the thing
myVal = symbol.method(something, test.var, 'some string');
";

            var lexer = new Lexer(program) { ParsingMode = ParsingMode.Breakout };
            var tokens = new List<Token>();

            var success = true;

            while (success)
            {
                var peekSuccess = lexer.TryPeekToken(out var peekToken);
                var consumeSuccess = lexer.TryConsumeToken(out var consumeToken);

                Assert.Equal(peekSuccess, consumeSuccess);
                Assert.Equal(peekToken, consumeToken);

                success = consumeSuccess;

                if (success)
                {
                    tokens.Add(consumeToken);
                }
            }

            var asserts = new List<System.Action<Token>>()
            {
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Comment, t),
                (t) => AssertToken(Symbol, "myVal", t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Operator, OperatorType.Assignment, t),
                (t) => AssertToken(WhiteSpace, t),
                (t) => AssertToken(Symbol, "symbol", t),
                (t) => AssertToken(Operator, OperatorType.MemberAccess, t),
                (t) => AssertToken(Symbol, "method", t),
                (t) => AssertToken(Paren, (ParenType.Round, ParenMode.Open), t),
                (t) => AssertToken(Symbol, "something", t),
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
    }
}
