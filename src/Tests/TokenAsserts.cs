using HttpScript.Parsing.Tokens;
using System;
using Xunit;
using static HttpScript.Parsing.Tokens.TokenType;

namespace Tests
{
    public class TokenAsserts
    {
        public static void AssertToken(TokenType expectedType, Token token)
            => AssertToken(expectedType, (object)null, token);

        public static void AssertToken<T>(TokenType expectedType, T expectedContent, Token token)
        {
            Assert.Equal(expectedType, token.Type);

            AssertContent(expectedContent, token);
        }

        public static void AssertContent(object expected, Token token)
        {
            object content = token switch
            {
                { Type: StringLiteral } => new string(token.GetValue<ReadOnlyMemory<char>>().Span),
                { Type: Symbol } => new string(token.GetValue<ReadOnlyMemory<char>>().Span),

                _ => token.Value,
            };

            Assert.Equal(expected, content);
        }
    }
}
