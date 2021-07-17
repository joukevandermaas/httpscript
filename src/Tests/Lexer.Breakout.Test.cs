using HttpScript.Parsing;
using HttpScript.Parsing.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Tests
{
    public class LexerBreakoutTest
    {
        [Theory]
        [InlineData(TokenType.WhiteSpace, " \t\n\r\n\r \n")]
        [InlineData(TokenType.Comment, "/* multiline\n\ncomment */")]
        [InlineData(TokenType.Comment, "/* nested /*multiline\n*/\ncomment */")]
        [InlineData(TokenType.Comment, "//single-line comment\n")]
        [InlineData(TokenType.Symbol, "_symbol", "_symbol")]
        [InlineData(TokenType.Symbol, "$symbol", "$symbol")]
        [InlineData(TokenType.Symbol, "symbol", "symbol")]
        [InlineData(TokenType.Symbol, "_SYMBOL", "_SYMBOL")]
        [InlineData(TokenType.Symbol, "$SYMBOL", "$SYMBOL")]
        [InlineData(TokenType.Symbol, "SYMBOL", "SYMBOL")]
        [InlineData(TokenType.String, "'✨'", "✨")]
        [InlineData(TokenType.String, "\"✨\"", "✨")]
        [InlineData(TokenType.Operator, "=", OperatorType.Assignment)]
        [InlineData(TokenType.Operator, ".", OperatorType.MemberAccess)]
        [InlineData(TokenType.Operator, ",", OperatorType.Separator)]
        [InlineData(TokenType.Paren, "(", ParenType.Open)]
        [InlineData(TokenType.Paren, ")", ParenType.Close)]
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
        [InlineData(TokenType.Comment, ErrorType.MissingEndComment, "/* comment")]
        [InlineData(TokenType.Comment, ErrorType.MissingEndComment, "/* /* /* comment */ */")]
        [InlineData(TokenType.String, ErrorType.MissingEndQuote, "\"✨", "✨")]
        [InlineData(TokenType.String, ErrorType.MissingEndQuote, "\'✨", "✨")]
        [InlineData(TokenType.String, ErrorType.MissingEndQuote, "\"✨\n", "✨")]
        [InlineData(TokenType.String, ErrorType.MissingEndQuote, "\'✨\n", "✨")]
        public void RecoversFromPredictableErrors(
            TokenType tokenType,
            ErrorType errorType,
            string program,
            object content = null)
        {
            var tokens = RunLexer(program);

            Assert.Collection(tokens,
                (t) => Assert.Equal(tokenType, t.Type),
                (t) => Assert.Equal(errorType, (t as ErrorToken).ErrorCode));

            var token = tokens.First();

            // the token should have the correct type
            Assert.Equal(tokenType, token.Type);

            AssertContent(content, token);
        }

        private static void AssertContent(object expected, Token token)
        {
            object content = token switch
            {
                { Type: TokenType.Error } => (token as ErrorToken).ErrorCode,
                { Type: TokenType.String } => (token as StringToken).Value,
                { Type: TokenType.Symbol } => (token as SymbolToken).Name,
                { Type: TokenType.Paren } => (token as ParenToken).ParenType,
                { Type: TokenType.Operator } => (token as OperatorToken).OperatorType,

                _ => null,
            };

            Assert.Equal(expected, content);
        }

        private static List<Token> RunLexer(string script) => new Lexer(script, breakoutOnly: true).GetTokens().ToList();
    }
}
