using HttpScript.Parsing;
using System;
using Xunit;

namespace Tests
{
    public class ParserTest
    {

        [Fact]
        public void ParsesProgram()
        {
            var program = @"
// assign the thing to the thing
myVal = 'test' // symbol.method(something, test.var, 'some string');
".AsMemory();

            var parser = new Parser(program);

            var result = parser.TryParse();

            Assert.True(result);
        }
    }
}
