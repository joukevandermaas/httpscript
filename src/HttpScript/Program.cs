using HttpScript.Parsing;
using System.Linq;

namespace HttpScript
{
    class Program
    {
        static void Main(string[] args)
        {
            var lexer = new Lexer("$survey");
            lexer.ParsingMode = ParsingMode.Breakout;

            var tokens = lexer.GetTokens().ToList();
        }
    }
}
