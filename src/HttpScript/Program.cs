using HttpScript.Parsing;
using System;
using System.Linq;

namespace HttpScript
{
    class Program
    {
        static void Main(string[] args)
        {
            var tokenizer = new Tokenizer("$survey".AsMemory());
            tokenizer.ParsingMode = ParsingMode.Breakout;

            var tokens = tokenizer.GetTokens().ToList();
        }
    }
}
