using System;
using System.Collections.Generic;
using System.Text;

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
                if (t.Value.TokenType == TokenType.IntegerConstant)
                {
                    Write($"push constant {t.Value.IntValue}");
                }
                else if (t.Value.TokenType == TokenType.StringConstant)
                {
                    var stringConstant = t.Value.Value;

                    Write($"push constant {stringConstant.Length}");
                    Write($"call String.new 1");
                    foreach (var chr in stringConstant)
                    {
                        Write($"push constant {(int)chr}");
                        Write($"call String.appendChar 2");
                    }                        
                }
                else if (t.Value.TokenType == TokenType.Keyword)
                {
                    string vmLine;
                    switch (t.Value.Value)
                    {
                        case "true":
                            vmLine = "push constant -1";
                            break;
                        case "false":
                            vmLine = "push constant 0";
                            break;
                        case "this":
                            vmLine = "push pointer 0";
                            break;
                        case "that":
                            vmLine = "push pointer 1";
                            break;
                        default:
                            throw new ArgumentException("Неизвестная Keyword constant");
                    }
                    Write(vmLine);
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
                    // Индексация !!!
                    var info = FindVarInfo(t.Value.Value);
                    var segmentName = info.SegmentName;
                    var segmentIndex = info.Index;
                    var index = t.Indexing.Index;

                    WriteExpression(index);
                    Write($"push {segmentName} {segmentIndex}");
                    Write("add");
                    Write($"pop pointer 1");
                    Write($"push that 0");
                    
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
                var t = (ParenthesizedTermSyntax)term;
                WriteExpression(t.Expression);
            }
            else if (term is SubroutineCallTermSyntax)
            {
                var t = (SubroutineCallTermSyntax)term;
                var call = t.Call;
                foreach (var arg in call.Arguments.DelimitedExpressions)
                    WriteExpression(arg);
                
                Write($"call {call.ObjectOrClass.Name.Value}.{call.SubroutineName.Value} {call.Arguments.DelimitedExpressions.Count}");
            }
            else 
                return false;

            return true;
        }
    }
}
