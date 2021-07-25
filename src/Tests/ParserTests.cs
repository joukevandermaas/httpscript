using HttpScript.Parsing;
using HttpScript.Parsing.Tokens;
using System;
using Xunit;

namespace Tests
{
    public class ParserTests
    {

        [Fact]
        public void ParsesProgram()
        {
            var program = @"
// assign the thing to the thing
myVal = symbol.method(321, test.var, 'some string');
".AsMemory();

            var token = new Token
            {
                Text = program,
                Value = program,
                Type = TokenType.Unknown,
            };

            var val = token.ToString();

            var parser = new Parser(program);

            var result = parser.TryParse();

            Assert.True(result);
        }
    }
}
