using System;

namespace JackCompiling
{
    public partial class CodeWriter
    {
        /// <summary>
        /// "string constant"
        /// </summary>
        private bool TryWriteStringValue(TermSyntax term)
        {
            if (term is not ValueTermSyntax || ((ValueTermSyntax)term).Value.TokenType != TokenType.StringConstant)
                return false;

            var valueTerm = term as ValueTermSyntax;

            var stringConstant = valueTerm.Value.Value;

            Write($"push constant {stringConstant.Length}");
            Write($"call String.new 1");
            foreach (var chr in stringConstant)
            {
                Write($"push constant {(int)chr}");
                Write($"call String.appendChar 2");
            }

            return true;
        }

        /// <summary>
        /// arr[index]
        /// </summary>
        private bool TryWriteArrayAccess(TermSyntax term)
        {
            return false;
        }

        /// <summary>
        /// let arr[index] = expr;
        /// </summary>
        private bool TryWriteArrayAssignmentStatement(StatementSyntax statement)
        {
            return false;
        }
    }
}
