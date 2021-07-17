using HttpScript.Parsing;
using HttpScript.Parsing.Tokens;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Tests
{
    public class LexerTest
    {
        [Theory]
        [InlineData(TokenType.WhiteSpace, " \t\n\r\n\r \n")]
        [InlineData(TokenType.Comment, "/* multiline\n\ncomment */")]
        [InlineData(TokenType.Comment, "/* nested /*multiline\n*/\ncomment */")]
        [InlineData(TokenType.Comment, "//single-line comment\n")]
        [InlineData(TokenType.Symbol, "_symbol")]
        [InlineData(TokenType.Symbol, "$symbol")]
        [InlineData(TokenType.Symbol, "symbol")]
        [InlineData(TokenType.Symbol, "_SYMBOL")]
        [InlineData(TokenType.Symbol, "$SYMBOL")]
        [InlineData(TokenType.Symbol, "SYMBOL")]
        [InlineData(TokenType.String, "'✨'")]
        [InlineData(TokenType.String, "\"✨\"")]
        public void RecognizesSingleToken(TokenType tokenType, string program)
        {
            var tokens = RunLexer(program);

            // should only be a single token
            Assert.Single(tokens);

            var token = tokens.Single();

            // the token should have the correct type
            Assert.Equal(tokenType, token.Type);

            // it should have consumed the whole input
            Assert.Equal(0, token.Range.StartOffset);
            Assert.Equal(program.Length, token.Range.EndOffset);
        }


        private static List<Token> RunLexer(string script) => new Lexer(script).GetTokens().ToList();
    }
}
