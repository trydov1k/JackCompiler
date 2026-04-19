using System;
using System.Collections.Generic;

namespace JackCompiling
{
    public partial class CodeWriter
    {
       public static Dictionary<string, string> OperatorToVmLine = new() 
       {
           { "+", "add" },
           { "-", "sub" },
           { "*", "call Math.multiply 2" },
           { "/", "call Math.divide 2" },
           { "|", "or" },
           { "&", "and" },
           { "<", "lt" },
           { ">", "gt" },
           { "=", "eq" }
       };
        /// <summary>2+x</summary>
        public void WriteExpression(ExpressionSyntax expression)
        {
            WriteTerm(expression.Term);
            WriteTail(expression.Tail);
        }

        private void WriteTerm(TermSyntax term)
        {
            var ok = TryWriteStringValue(term) // будет реализована в следующих задачах
                     || TryWriteArrayAccess(term) // будет реализована в следующих задачах
                     || TryWriteObjectValue(term) // будет реализована в следующих задачах
                     || TryWriteSubroutineCall(term) // будет реализована в следующих задачах
                     || TryWriteNumericTerm(term);
            if (!ok)
                throw new FormatException($"Unknown term [{term}]");
        }

        private void WriteTail(IReadOnlyList<ExpressionTail> tails)
        {
            foreach (var tail in tails)
            {
                WriteTerm(tail.Term);
                var op = tail.Operator.Value;
                Write(OperatorToVmLine[op]);
            }
        }

        /// <summary>42 | true | false | varName | -x | ( x )</summary>
        private bool TryWriteNumericTerm(TermSyntax term)
        {
            if (term is ValueTermSyntax)
            {
                var t = (ValueTermSyntax)term;
                if (t.Value.TokenType != TokenType.Identifier)
                {
                    var value = (t.Value.Value == "true") ? "-1" : (t.Value.Value == "false") ? "0" : t.Value.Value;
                    Write($"push constant {value}");
                }
                else if (t.Indexing == null)
                {
                    var info = FindVarInfo(t.Value.Value);
                    var segmentName = info.SegmentName;
                    var index = info.Index;
                    Write($"push {segmentName} {index}");
                }
                else
                {

                }
            }
            else if (term is UnaryOpTermSyntax)
            {
                var t = (UnaryOpTermSyntax)term;
                var negOrNot = t.UnaryOp.Value == "-" ? "neg" : "not";
                WriteTerm(t.Term);
                Write(negOrNot);
            }
            else if (term is ParenthesizedTermSyntax)
            {

            }
            else if (term is SubroutineCallTermSyntax)
            {

            }
            else 
                return false;

            return true;
        }
    }
}
