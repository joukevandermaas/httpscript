using HttpScript.Parsing;
using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;
using Xunit;
using static HttpScript.Parsing.Tokens.TokenType;
using static Tests.TokenAsserts;

namespace Tests
{
    public class TokenizerTests
    {

        [Fact]
        public void PeekReturnsSameAsConsume()
        {
            var program = @"
// assign the thing to the thing
myVal = symbol.method(something, test.var, 'some string');
".AsMemory();

            var tokenizer = new Tokenizer(program) { ParsingMode = ParsingMode.Breakout };
            var tokens = new List<Token>();

            var success = true;

            while (success)
            {
                var peekSuccess = tokenizer.TryPeekToken(out var peekToken);
                var consumeSuccess = tokenizer.TryConsumeToken(out var consumeToken);

                Assert.Equal(peekSuccess, consumeSuccess);
                Assert.Equal(peekToken, consumeToken);

                success = consumeSuccess;

                if (success)
                {
                    tokens.Add(consumeToken);
                }
            }

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
    }
}
