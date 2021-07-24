using HttpScript.Parsing.Tokens;
using System;
using static HttpScript.Parsing.Tokens.TokenType;

namespace HttpScript.Parsing
{
    public class Parser
    {
        private readonly Lexer lexer;

        private ParsingMode mode;

        public Parser(ReadOnlyMemory<char> program)
        {
            this.lexer = new Lexer(program)
            {
                ParsingMode = ParsingMode.Breakout
            };
        }

        // used for debugging
        private Token NextToken
        {
            get
            {
                _ = this.lexer.TryPeekToken(out var token);
                return token;
            }
        }

        public static void Parse(ReadOnlyMemory<char> program) => new Parser(program).TryParse();

        public bool TryParse()
        {
            while (!this.lexer.IsComplete)
            {
                this.OptionalCommentOrWhiteSpace();

                if (this.lexer.IsComplete)
                {
                    // file ended with whitespace/comment
                    break;
                }

                if (!this.TryParseExpression())
                {
                    // report error
                    return false;
                }
            }

            return true;
        }

        private void OptionalCommentOrWhiteSpace()
        {
            while (
                this.lexer.TryConsumeTokenOfType(WhiteSpace, out var _) ||
                this.lexer.TryConsumeTokenOfType(Comment, out var _)
            ) ;
        }

        private bool TryParseExpression()
        {
            var result =
                this.TryParseAssignment() ||
                this.TryParseSimpleValue();

            return result;
        }

        private bool TryParseAssignment()
        {
            if (!this.lexer.TryConsumeTokenOfType(Symbol, out _))
            {
                return false;
            }

            this.lexer.PushRestorePoint();

            this.OptionalCommentOrWhiteSpace();

            if (!this.lexer.TryConsumeTokenOfType<OperatorToken>(out var opToken)
                || opToken.OperatorType != OperatorType.Assignment)
            {
                this.lexer.PopRestorePoint();
                return false;
            }

            this.OptionalCommentOrWhiteSpace();

            if (!this.TryParseExpression())
            {
                this.lexer.PopRestorePoint();
                return false;
            }

            this.lexer.DiscardRestorePoint();
            return true;
        }

        private bool TryParseSimpleValue()
        {
            var result =
                this.lexer.TryConsumeTokenOfType(StringContent, out _) ||
                this.lexer.TryConsumeTokenOfType(Symbol, out _);

            return result;
        }
    }
}
