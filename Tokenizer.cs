using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JackCompiling
{
    public class Tokenizer
    {
        private readonly char[] symbols = { 
            '{', '}', '(', ')', '[', ']', '<', '>',
            '.', ',', ';', '+', '-', '*', '/', '&', '|', '=', '~' 
        };
        private readonly char[] digits = { 
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' 
        };
        private readonly string[] keywords = { 
            "class", "constructor", "function", "method", "field", "static", "var",
            "int", "char", "boolean", "void", "true", "false", "null", "this", "let", 
            "do", "if", "else", "while", "return", 
        };


        private readonly Queue<string> _textQueue;
        private Stack<Token> _pushBackStack;

        private StringBuilder savedLine;
        private bool IsMultiplineCommentOpen;

        public Tokenizer(string text)
        {
            _pushBackStack = new Stack<Token>();
            savedLine = new StringBuilder();
            _textQueue = new Queue<string>();

            foreach (var line in text.Split('\n'))
                _textQueue.Enqueue(line);
        }

        /// <summary>
        /// Сначала возвращает все токены, которые вернули методом PushBack в порядке First In Last Out.
        /// Потом читает и возвращает один следующий токен, либо null, если больше токенов нет.
        /// Пропускает пробелы и комментарии.
        ///
        /// Хорошо, если внутри Token сохранит ещё и строку и позицию в исходном тексте. Но это не проверяется тестами.
        /// </summary>
        public Token? TryReadNext()
        {
            if (_pushBackStack.Count > 0)
                return _pushBackStack.Pop();

            while (savedLine.Length == 0)
            {
                if (_textQueue.Count == 0)
                    return null;
                savedLine = SkipWhiteSpacesAndComments(_textQueue.Dequeue());

                while (savedLine.ToString().Contains("//") || savedLine.ToString().Contains("/*"))
                    savedLine = SkipWhiteSpacesAndComments(savedLine.ToString());

                if (savedLine.Length != 0 && savedLine[0] == ' ')
                    savedLine.Remove(0, 1);
            }
                
            var token = TryReadSymbol(savedLine)
                ?? TryReadStringConstant(savedLine)
                ?? TryReadIntegerConstant(savedLine)
                ?? TryReadIdentifier(savedLine);
                
            return token;
        }

        /// <summary>
        /// Откатывает токенайзер на один токен назад.
        /// Если token - null, то игнорирует его и никуда не возвращает.
        /// Поддержка null нужна для удобства, чтобы использовать TryReadNext, вместе с PushBack без лишних if-ов.
        /// </summary>
        public void PushBack(Token? token)
        {
            if (token !=  null)
                _pushBackStack.Push(token);
        }

        private Token? TryReadSymbol(StringBuilder savedLine)
        {
            while (savedLine.Length != 0 && savedLine[0] == ' ')
                savedLine.Remove(0, 1);

            var chr = savedLine[0];
            if (symbols.Contains(chr))
            {
                savedLine.Remove(0, 1);
                return new Token(TokenType.Symbol, chr.ToString(), 1, 1);
            }

            return null;
        }

        private Token? TryReadStringConstant(StringBuilder savedLine)
        {
            if (savedLine.Length != 0 && savedLine[0] == ' ')
                savedLine.Remove(0, 1);

            var value = new StringBuilder();
            var chr = savedLine[0];

            if (chr == '\"')
            {
                savedLine.Remove(0, 1);
                while (savedLine[0] != '\"')
                {
                    value.Append(savedLine[0]);
                    savedLine.Remove(0, 1);
                }
                savedLine.Remove(0, 1);

                return new Token(TokenType.StringConstant, value.ToString(), 1, 1);
            }

            return null;
        }

        private Token? TryReadIntegerConstant(StringBuilder savedLine)
        {
            if (savedLine.Length != 0 && savedLine[0] == ' ')
                savedLine.Remove(0, 1);

            var value = new StringBuilder();

            while (savedLine.Length != 0 && digits.Contains(savedLine[0]))
            {
                value.Append(savedLine[0]);
                savedLine.Remove(0, 1);
            }

            if (value.Length != 0)
                return new Token(TokenType.IntegerConstant, value.ToString(), 1, 1);

            return null;
        }

        private Token? TryReadIdentifier(StringBuilder savedLine)
        {
            if (savedLine.Length != 0 && savedLine[0] == ' ')
                savedLine.Remove(0, 1);

            var value = new StringBuilder();

            while (savedLine.Length != 0 && savedLine[0] != ' ' && !symbols.Contains(savedLine[0]))
            {
                value.Append(savedLine[0]);
                savedLine.Remove(0, 1);
            }

            if (value.Length == 0 || value.ToString() == " ")
                return null;

            if (!keywords.Contains(value.ToString()))
                return new Token(TokenType.Identifier, value.ToString(), 1, 1);

            return new Token(TokenType.Keyword, value.ToString(), 1, 1);
        }

        private StringBuilder SkipWhiteSpacesAndComments(string line)
        {
            TakeIndexes(line, out int indOfComm, out int indOfMultiCommLeft, out int indOfMultiCommRight);
            if (indOfComm >= 0)
                line = line.Remove(indOfComm);
            if (indOfMultiCommLeft >= 0)
            {
                IsMultiplineCommentOpen = true;                
                if (indOfMultiCommRight >= 0)
                {
                    IsMultiplineCommentOpen = false;
                    return new StringBuilder(line.Remove(indOfMultiCommLeft, 
                        indOfMultiCommRight - indOfMultiCommLeft + 2).Trim());
                }
                return new StringBuilder(line.Remove(indOfMultiCommLeft).Trim());
            }
            if (IsMultiplineCommentOpen && indOfMultiCommRight >= 0)
            {
                IsMultiplineCommentOpen = false;
                return new StringBuilder(line.Remove(0, indOfMultiCommRight + 2).Trim());
            }
            else if (IsMultiplineCommentOpen) return new StringBuilder();
            return ReturnInputString(line);
        }

        private void TakeIndexes(string line, out int indexOfBasicComment, 
            out int indexOfMultilineCommentLeft, out int indexOfMultilineCommentRight)
        {
            indexOfBasicComment = line.IndexOf("//");
            indexOfMultilineCommentLeft = line.IndexOf("/*");
            indexOfMultilineCommentRight = line.IndexOf("*/");
        }

        private StringBuilder ReturnInputString(string line)
        {
            var sb = new StringBuilder();
            foreach (var chr in line.Trim())
                sb.Append(chr);
            return sb;
        }
    }
}
