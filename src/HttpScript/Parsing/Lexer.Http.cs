using HttpScript.Parsing.Tokens;

namespace HttpScript.Parsing
{
    public partial class Lexer
    {
        private bool TryGetHttpToken(out Token token, out ErrorToken? errorToken)
        {
            errorToken = null;

            if (TryGetWhiteSpaceToken(out token)) { return true; }

            // we don't know wtf is going on, we should let the main
            // loop deal with this tho, we don't have enough info to
            // emit an error token here
            return false;
        }
    }
}
