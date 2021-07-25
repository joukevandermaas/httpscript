using HttpScript.Parsing.Tokens;
using System;
using static HttpScript.Parsing.Tokens.TokenType;

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
            while (!this.tokenizer.HasMoreTokens)
            {
                this.OptionalCommentOrWhiteSpace();

                if (this.tokenizer.HasMoreTokens)
                {
                    // file ended with whitespace/comment
                    break;
                }

                if (!this.Expression())
                {
                    // report error
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// OptionalCommentOrWhiteSpace
        ///   : opt(COMMENT | WHITESPACE)
        /// </summary>
        private void OptionalCommentOrWhiteSpace()
        {
            while (
                this.tokenizer.TryConsumeTokenOfType(WhiteSpace, out var _) ||
                this.tokenizer.TryConsumeTokenOfType(Comment, out var _)
            ) ;
        }

        private bool Expression()
        {
            var result =
                this.Assignment() ||
                this.Literal();

            return result;
        }

        /// <summary>
        /// Assignment
        ///   : SYMBOL = Expression
        /// </summary>
        private bool Assignment()
        {
            if (!this.tokenizer.TryConsumeTokenOfType(Symbol, out _))
            {
                return false;
            }

            this.tokenizer.PushRestorePoint();

            this.OptionalCommentOrWhiteSpace();

            if (!this.tokenizer.TryConsumeTokenOfType<OperatorToken>(out var opToken)
                || opToken.OperatorType != OperatorType.Assignment)
            {
                this.tokenizer.PopRestorePoint();
                return false;
            }

            this.OptionalCommentOrWhiteSpace();

            if (!this.Expression())
            {
                this.tokenizer.PopRestorePoint();
                return false;
            }

            this.tokenizer.DiscardRestorePoint();
            return true;
        }

        /// <summary>
        /// Literal
        ///   : STRING_LITERAL | NUMBER_LITERAL | SYMBOL
        /// </summary>
        private bool Literal()
        {
            var result =
                this.tokenizer.TryConsumeTokenOfType(StringLiteral, out _)
                || this.tokenizer.TryConsumeTokenOfType(NumberLiteral, out _)
                || this.tokenizer.TryConsumeTokenOfType(Symbol, out _);

            return result;
        }
    }
}
