using HttpScript.Parsing.Tokens;
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
                { Type: Error } => (token as ErrorToken).ErrorCode,
                { Type: String } => (token as StringToken).Value,
                { Type: Symbol } => (token as SymbolToken).Name,
                { Type: Paren } => ((token as ParenToken).ParenType, (token as ParenToken).ParenMode),
                { Type: Operator } => (token as OperatorToken).OperatorType,

                _ => null,
            };

            Assert.Equal(expected, content);
        }
    }
}
