using System;
using System.Collections.Generic;
using System.Reflection;

namespace JackCompiling
{
    public partial class CodeWriter
    {
        private static Dictionary<string, string> OperatorToVmLine = new() 
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

        private static Dictionary<string, string> UnaryOpToVmLine = new()
        {
            { "-", "neg" },
            { "~", "not" }
        };

        private static Dictionary<string, string> KeywordConstantToVm = new()
        {
            { "true", "push constant -1" },
            { "false", "push constant 0" },
            { "this", "push pointer 0" },
            { "that", "push pointer 1" }
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
            if (term is ValueTermSyntax)  // true | 42 | x | "hello"
                WriteValueTermSyntax((ValueTermSyntax)term);
            else if (term is UnaryOpTermSyntax)  // -x | ~y
                WriteUnaryOpTermSyntax((UnaryOpTermSyntax)term);
            else if (term is ParenthesizedTermSyntax)  // (x+y)
                WriteParenthesizedTermSyntax((ParenthesizedTermSyntax)term);
            else
                return false;  // Если это неизвестная штучка, возвращаем false

            return true;
        }

        private void WriteValueTermSyntax(ValueTermSyntax term)
        {
            switch (term.Value.TokenType)
            {
                case TokenType.IntegerConstant:
                    Write($"push constant {term.Value.IntValue}");
                    break;
                case TokenType.Keyword:
                    var vmLine = KeywordConstantToVm[term.Value.Value];
                    Write(vmLine);
                    break;
                case TokenType.Identifier:
                    var info = FindVarInfo(term.Value.Value);
                    var segmentName = info.SegmentName;
                    var segmentIndex = info.Index;

                    if (term.Indexing != null)
                    {
                        var index = term.Indexing.Index;

                        WriteIndexValueToStack(index, segmentName, segmentIndex);
                        Write($"push that 0");
                    }
                    else
                        Write($"push {segmentName} {segmentIndex}");
                    break;
            }
        }

        private void WriteUnaryOpTermSyntax(UnaryOpTermSyntax term)
        {
            var opVmLine = UnaryOpToVmLine[term.UnaryOp.Value];
            WriteTerm(term.Term);
            Write(opVmLine);
        }

        private void WriteParenthesizedTermSyntax(ParenthesizedTermSyntax term)
        {
            WriteExpression(term.Expression);
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
