using HttpScript.Parsing;
using System.Linq;

namespace HttpScript
{
    class Program
    {
        static void Main(string[] args)
        {
            var lexer = new Lexer("$survey", breakoutOnly: false);

            var tokens = lexer.GetTokens().ToList();
        }
    }
}
