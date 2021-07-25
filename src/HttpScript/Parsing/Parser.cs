using HttpScript.Parsing.Tokens;
using System;

namespace HttpScript.Parsing
{
    //public class AstNode
    //{
    //    public NodeType Type { get; }

    //}

    public class Parser
    {
        private readonly Tokenizer tokenizer;

        private ParsingMode mode;

        public Parser(ReadOnlyMemory<char> program)
        {
            this.tokenizer = new Tokenizer(program)
            {
                ParsingMode = ParsingMode.Breakout
            };
        }

        // used for debugging
        private Token NextToken
        {
            get
            {
                _ = this.tokenizer.TryPeekToken(out var token);
                return token;
            }
        }

        public static void Parse(ReadOnlyMemory<char> program) => new Parser(program).TryParse();

        public bool TryParse()
        {
            return Program();
        }

        private bool Program()
        {
            while (this.HasMoreTokens)
            {
                if (!this.Expression())
                {
                    // report error
                    return false;
                }

                var nextToken = this.PeekToken();
                if (nextToken.Type != TokenType.Separator || nextToken.GetValue<char>() != ';')
                {
                    // report error
                    return false;
                }

                // consume semicolon
                this.ConsumeToken();
            }

            return true;
        }

        private bool Expression()
        {
            var result = this.Literal();

            if (this.PeekToken().Type == TokenType.BinaryOperator)
            {
                BinaryOperatorExpression();
            }

            var nextToken = this.PeekToken();
            if (nextToken.Type == TokenType.Paren && nextToken.GetValue<char>() == '(')
            {
                FunctionInvocation();
            }

            return result;
        }

        private bool FunctionInvocation()
        {
            this.tokenizer.PushRestorePoint();

            if (!this.TryConsumeTokenOfType(TokenType.Paren, out var paren) || paren.GetValue<char>() != '(')
            {
                this.tokenizer.PopRestorePoint();
                return false;
            }

            var nextToken = this.PeekToken();
            var isFirst = true;

            while (nextToken.Type != TokenType.Paren || nextToken.GetValue<char>() != ')')
            {
                if (!isFirst)
                {
                    // we expect a comma to separate the parameters
                    if (!this.TryConsumeTokenOfType(TokenType.Separator, out var sep) || sep.GetValue<char>() != ',')
                    {
                        this.tokenizer.PopRestorePoint();
                        return false;
                    }
                }
                else
                {
                    isFirst = false;
                }

                // we expect an expression
                this.Expression();

                nextToken = this.PeekToken();
            }

            // consume the close paren
            this.ConsumeToken();
            this.tokenizer.DiscardRestorePoint();
            return true;
        }

        private bool BinaryOperatorExpression()
        {
            this.tokenizer.PushRestorePoint();

            if (!this.TryConsumeTokenOfType(TokenType.BinaryOperator, out _))
            {
                this.tokenizer.PopRestorePoint();
                return false;
            }

            if (!this.Expression())
            {
                this.tokenizer.PopRestorePoint();
                return false;
            }

            this.tokenizer.DiscardRestorePoint();
            return true;
        }

        private bool Literal()
        {
            var result =
                this.TryConsumeTokenOfType(TokenType.StringLiteral, out _)
                || this.TryConsumeTokenOfType(TokenType.NumberLiteral, out _)
                || this.TryConsumeTokenOfType(TokenType.Symbol, out _);

            return result;
        }

        private bool HasMoreTokens => PeekToken() == default;

        private Token PeekToken()
        {
            Token token;

            while (this.tokenizer.TryPeekToken(out token)
                && (token.Type == TokenType.Comment || token.Type == TokenType.WhiteSpace || token.Type == TokenType.Error))
            {
                // TODO handle errors
                this.tokenizer.TryConsumeToken(out _);
            }

            return token;
        }

        private Token ConsumeToken()
        {
            var token = this.PeekToken();
            this.tokenizer.TryConsumeToken(out _);

            return token;
        }

        private bool TryConsumeTokenOfType(TokenType tokenType, out Token token)
        {
            var peekToken = this.PeekToken();
            
            if (peekToken.Type == tokenType)
            {
                token = peekToken;
                this.tokenizer.TryConsumeToken(out _);
                return true;
            }

            token = default;
            return false;
        }
    }
}
