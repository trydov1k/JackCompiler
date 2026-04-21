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
            var valueTerm = term as ValueTermSyntax;

            if (valueTerm == null || valueTerm.Indexing == null || valueTerm.Value.TokenType != TokenType.Identifier)
                return false;

            var info = FindVarInfo(valueTerm.Value.Value);
            var segmentName = info.SegmentName;
            var segmentIndex = info.Index;

            var index = valueTerm.Indexing.Index;

            WriteIndexValueToStack(index, segmentName, segmentIndex);
            Write($"push that 0");


            return true;
        }

        /// <summary>
        /// let arr[index] = expr;
        /// </summary>
        private bool TryWriteArrayAssignmentStatement(StatementSyntax statement)
        {
            return false;
        }

        private void WriteIndexValueToStack(ExpressionSyntax index, string segmentName, int segmentIndex)
        {
            WriteExpression(index);
            Write($"push {segmentName} {segmentIndex}");
            Write("add");
            Write($"pop pointer 1");
        }
    }
}
