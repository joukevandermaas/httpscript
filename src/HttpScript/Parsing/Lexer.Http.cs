using HttpScript.Parsing.Tokens;

namespace HttpScript.Parsing
{
    public partial class Lexer
    {
        private bool TryGetHttpToken(out Token token)
        {
            if (TryGetWhiteSpaceToken(out token)) { return true; }

            return false;
        }
    }
}
