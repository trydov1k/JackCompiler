using System;
using System.Collections.Generic;
using System.Linq;

namespace JackCompiling
{
    public class Parser
    {
        #region Конструктор
        private readonly Tokenizer tokenizer;

        public Parser(Tokenizer tokenizer)
        {
            this.tokenizer = tokenizer;
        }
        #endregion

        #region Обработка класса
        public ClassSyntax ReadClass()
        {
            var classToken = tokenizer.Read("class");                   // class
            var classNameToken = tokenizer.Read(TokenType.Identifier);  // className
            var openToken = tokenizer.Read("{");                        // {
            var nextToken = tokenizer.Read();
            var classVars = new List<ClassVarDecSyntax>();
            while (nextToken.Value == "static" || nextToken.Value == "field")
            {
                tokenizer.PushBack(nextToken);      
                classVars.Add(ReadClassVarDec());                       // classVarDec
                nextToken = tokenizer.Read();
            }            
            var subrounineDecList = new List<SubroutineDecSyntax>();
            while (nextToken.Value == "constructor" || nextToken.Value == "function" || nextToken.Value == "method")
            {
                tokenizer.PushBack(nextToken);
                subrounineDecList.Add(ReadSubroutineDec());            // subroutineDec
                nextToken = tokenizer.Read();
            }
            tokenizer.PushBack(nextToken);
            var closeToken = tokenizer.Read("}");                       // }
            return new ClassSyntax(classToken, classNameToken, openToken, classVars, 
                subrounineDecList, closeToken);  // class className { classVarDec* subroutineDec* }
        }

        public ClassVarDecSyntax ReadClassVarDec()
        {
            var token = tokenizer.Read(TokenType.Keyword);              // static | field
            if (token.Value != "static" && token.Value != "field")
                throw new ExpectedException("static or field", token);

            var nextToken = tokenizer.Read();

            var typeToken = nextToken;                                  // type

            if (!IsTypeToken(nextToken))
            {
                throw new ExpectedException("int or char or bolean or identifier", nextToken);
            }

            var varNameTokens = tokenizer.ReadDelimitedList(() => tokenizer.Read(TokenType.Identifier), ",", ";");

            var semicolon = tokenizer.Read(";");

            return new ClassVarDecSyntax(token, typeToken, varNameTokens, 
                semicolon);  // (static | field) type varName (, varname)* ;
        }

        public SubroutineDecSyntax ReadSubroutineDec()
        {
            var token = tokenizer.Read(TokenType.Keyword);              // constructor | function | method
            if (token.Value != "constructor" && token.Value != "function" && token.Value != "method")
                throw new ExpectedException("constructor or function or method", token);

            var nextToken = tokenizer.Read();

            var typeToken = nextToken;                                  // type

            if (!IsTypeToken(nextToken))
            {
                if (nextToken.Value != "void")
                    throw new ExpectedException("int or char or bolean", nextToken);
            }

            var subroutineName = tokenizer.Read(TokenType.Identifier);  // subroutineName
            var openToken = tokenizer.Read("(");                        // (
            var parameterList = ReadParameterList();                    // parameterList
            var closeToken = tokenizer.Read(")");                       // )
            var subroutineBody = ReadSubroutineBody();                  // subroutineBody

            return new SubroutineDecSyntax(token, typeToken, subroutineName, openToken, 
                parameterList, closeToken, subroutineBody);
        }
        
        public SubroutineBodySyntax ReadSubroutineBody()
        {
            var open = tokenizer.Read("{");
            var varDecList = new List<VarDecSyntax>();

            var nextToken = tokenizer.Read();
            

            while (nextToken.Value == "var")
            {
                tokenizer.PushBack(nextToken);
                var varDec = ReadVarDec();
                varDecList.Add(varDec);
                nextToken = tokenizer.Read();
            }
            tokenizer.PushBack(nextToken);

            var statements = ReadStatements();
            var close = tokenizer.Read("}");

            return new SubroutineBodySyntax(open, varDecList, statements, close);
        }
        
        public VarDecSyntax ReadVarDec()
        {
            var varToken = tokenizer.Read("var");  // var

            var nextToken = tokenizer.Read();

            var typeToken = nextToken;             // type

            if (!IsTypeToken(nextToken))
            {
                throw new ExpectedException("int or char or bolean", nextToken);
            }

            var varNameTokens = tokenizer.ReadDelimitedList(() => tokenizer.Read(), ",", ";");

            var semicolon = tokenizer.Read(";");

            return new VarDecSyntax(varToken, typeToken, varNameTokens, semicolon);
        }
        
        public ParameterListSyntax ReadParameterList()
        {
            var parameters = new List<Parameter>();
            var nextToken = tokenizer.Read();
            var go = true;
            if (nextToken.Value == ")")
                go = false;
            while (go)
            {
                var typeToken = nextToken;
                if (!IsTypeToken(typeToken)) 
                    throw new ExpectedException("int or char or boolean or identidier", typeToken);
                var varName = tokenizer.Read(TokenType.Identifier);
                var nextNextToken = tokenizer.Read();
                if (nextNextToken.Value != ",")
                {
                    go = false;
                    tokenizer.PushBack(nextNextToken);
                    if (nextNextToken.Value != ")") throw new ExpectedException(")", nextNextToken);
                }
                nextToken = tokenizer.Read();
                parameters.Add(new Parameter(typeToken, varName));
            }
            tokenizer.PushBack(nextToken);
            return new ParameterListSyntax(parameters);
        }
        #endregion

        #region Обработка statements
        public StatementsSyntax ReadStatements()
        {
            var token = tokenizer.TryReadNext();  // let / if / while / do / return
            var statements = new List<StatementSyntax>();

            if (token == null)
                return new StatementsSyntax(statements);

            tokenizer.PushBack(token);
            while (IsStatement(token))
            {
                switch (token.Value)
                {
                    case "let":
                        statements.Add(ReadLetStatement());
                        break;
                    case "if":
                        statements.Add(ReadIfStatement());
                        break;
                    case "while":
                        statements.Add(ReadWhileStatement());
                        break;
                    case "do":
                        statements.Add(ReadDoStatement());
                        break;
                    case "return":
                        statements.Add(ReadReturnStatement());
                        break;
                }

                token = tokenizer.TryReadNext();
                if (token == null)
                    break;
                tokenizer.PushBack(token);
            }

            return new StatementsSyntax(statements);
        }

        public LetStatementSyntax ReadLetStatement()
        {
            var letToken = tokenizer.Read("let");         // let
            var varNameToken = tokenizer.Read(TokenType.Identifier);  // varName
            var nextToken = tokenizer.Read(TokenType.Symbol);         // [  или  =

            Indexing? index = null;
            if (nextToken.Value == "[")
            {
                var expression = ReadExpression();    // expression
                var close = tokenizer.Read("]");      // ]

                index = new Indexing(nextToken, expression, close);  // [ expression ]
            }
            Token equalToken = nextToken;  // =
            if (index != null) 
                equalToken = tokenizer.Read("=");
            if (equalToken.Value != "=")
                throw new ExpectedException("=", equalToken);

            var value = ReadExpression();  // expression
            var semicolon = tokenizer.Read(";");  // ;

            return new LetStatementSyntax(letToken, varNameToken, index, 
                equalToken, value, semicolon);  // let varName ([ expression ])? = expression ;
        }

        public IfStatementSyntax ReadIfStatement() 
        {
            var ifToken = tokenizer.Read("if");          // if
            var openToken = tokenizer.Read("(");         // (
            var condition = ReadExpression();            // expression
            var closeToken = tokenizer.Read(")");        // )
            var figureOpenToken = tokenizer.Read("{");   // {
            var statements = ReadStatements();           // statements
            var figureCloseToken = tokenizer.Read("}");  // }

            ElseClause? elseClause = null;
            var nextToken = tokenizer.TryReadNext();
            if (nextToken != null && nextToken.Value == "else")
            {
                var openElseToken = tokenizer.Read("{");   // {
                var statementsElse = ReadStatements();     // statements
                var closeElseToken = tokenizer.Read("}");  // }

                elseClause = new ElseClause(nextToken, openElseToken, 
                    statementsElse, closeElseToken);  // else { statements }
            }
            if (elseClause == null)
                tokenizer.PushBack(nextToken);
            return new IfStatementSyntax(ifToken, openToken, condition, closeToken, 
                figureOpenToken, statements, figureCloseToken, elseClause);
        }
        
        public WhileStatementSyntax ReadWhileStatement()
        {
            var whileToken = tokenizer.Read("while");    // while
            var openToken = tokenizer.Read("(");         // (
            var condition = ReadExpression();            // expression
            var closeToken = tokenizer.Read(")");        // )
            var figureOpenToken = tokenizer.Read("{");   // {
            var statments = ReadStatements();            // statements
            var figureCloseToken = tokenizer.Read("}");  // }

            return new WhileStatementSyntax(whileToken, openToken, condition, 
                closeToken, figureOpenToken, statments, figureCloseToken);  // while ( expression ) { statements }
        }
        
        public DoStatementSyntax ReadDoStatement()
        {
            var doToken = tokenizer.Read("do");       // do

            var subrutineCall = ReadSubroutineCall();    // subrutineCall

            var semicolon = tokenizer.Read(";");     // ;
            return new DoStatementSyntax(doToken, subrutineCall, semicolon);  // do subrutineCall ;
        }
        
        public ReturnStatementSyntax ReadReturnStatement()
        {
            var returnToken = tokenizer.Read("return");  // return

            var nextToken = tokenizer.Read();

            bool haveExpression = HaveExpression(nextToken);

            if (!haveExpression)
            {
                var semicolon0 = tokenizer.Read(";");
                return new ReturnStatementSyntax(returnToken, null, semicolon0);
            }

            var expression = ReadExpression();
            var semicolon = tokenizer.Read(";");

            return new ReturnStatementSyntax(returnToken, expression, semicolon);
        }
        
        public SubroutineCall ReadSubroutineCall()
        {
            var token = tokenizer.Read(TokenType.Identifier);  // subroutineName | (className | varName)

            var nextToken = tokenizer.Read(TokenType.Symbol);  // (  или  .

            if (nextToken.Value == "(")
            {
                var expressionList0 = ReadExpressionList();  // expressionList
                var close0 = tokenizer.Read(")");            // )
                return new SubroutineCall(null, token, nextToken, expressionList0, close0);
            }
            else if (nextToken.Value == ".")
            {
                var subroutineName = tokenizer.Read(TokenType.Identifier);  // subroutineName
                var open = tokenizer.Read("(");                             // (
                var expressionList = ReadExpressionList();                  // expressionList
                var close = tokenizer.Read(")");                            // )

                var methodOjectOrClass = new MethodObjectOrClass(token, nextToken);

                return new SubroutineCall(methodOjectOrClass, subroutineName, open, expressionList, close);
            }

            throw new ExpectedException("( или .", nextToken);
        }

        public ExpressionSyntax ReadExpression()
        {
            var ops = new string[] { "+", "-", "*", "/", "&", "|", "<", ">", "=" };

            var term = ReadTerm();

            List<ExpressionTail> tail = new();

            var nextToken = tokenizer.Read();

            while (ops.Contains(nextToken.Value))
            {
                var op = nextToken;
                var trm = ReadTerm();
                tail.Add(new ExpressionTail(op, trm));
                nextToken = tokenizer.Read();
            }
            tokenizer.PushBack(nextToken);

            return new ExpressionSyntax(term, tail);
        }

        public TermSyntax ReadTerm()
        {
            var token = tokenizer.Read();

            switch (token.TokenType)
            {
                case TokenType.IntegerConstant:
                case TokenType.StringConstant:
                    return new ValueTermSyntax(token, null);  // constant

                case TokenType.Keyword:
                    if (token.Value == "true" || token.Value == "false" 
                        || token.Value == "null" || token.Value == "this")
                        return new ValueTermSyntax(token, null);  // true | false | null | this
                    throw new Exception("Неправильная константа");

                case TokenType.Symbol:
                    if (token.Value == "-" || token.Value == "~")
                    {
                        var term = ReadTerm();
                        return new UnaryOpTermSyntax(token, term);  // - term  |  ~ term
                    }
                    else if (token.Value == "(")
                    {
                        var open = token;                   // (
                        var expression = ReadExpression();  // expression
                        var close = tokenizer.Read(")");    // )
                        return new ParenthesizedTermSyntax(open, expression, close);  // ( expression )
                    }

                    throw new ExpectedException("Неправильная константа", token);

                case TokenType.Identifier:
                    var varName = token;

                    Indexing? index = null;
                    var nextToken = tokenizer.TryReadNext();
                    if (nextToken == null)
                        return new ValueTermSyntax(varName, null);

                    if (nextToken.Value == "[")  // [ expression ]
                    {
                        var open = nextToken;               // [
                        var expression = ReadExpression();  // expression
                        var close = tokenizer.Read("]");    // ]
                        index = new Indexing(open, expression, close);  // [ expression ]
                        return new ValueTermSyntax(token, @index);
                    }
                    else if (nextToken.Value == "(" || nextToken.Value == ".")  // subroutineCall
                    {
                        tokenizer.PushBack(nextToken);
                        tokenizer.PushBack(token);
                        var subroutineCall = ReadSubroutineCall();

                        return new SubroutineCallTermSyntax(subroutineCall);
                    }
                    tokenizer.PushBack(nextToken);
                    return new ValueTermSyntax(token, null);  // varName

                default:
                    throw new Exception("Ошибка, не могу спарсить данные, это точно term?");
            }
        }
        
        public ExpressionListSyntax ReadExpressionList()
        {
            var expressionList = new List<ExpressionSyntax>();

            var token = tokenizer.Read();
            var haveExpression = HaveExpression(token);

            while (haveExpression)
            {
                expressionList.Add(ReadExpression());
                var comma = tokenizer.Read();
                if (comma.Value != ",")
                {
                    tokenizer.PushBack(comma);
                    haveExpression = false;
                }
            }

            return new ExpressionListSyntax(expressionList);
        }
        #endregion

        #region Мои частные методы
        private bool IsTypeToken(Token token)
        {
            if (token.TokenType == TokenType.Keyword)
                return token.Value == "int" || token.Value == "char" || token.Value == "boolean";

            return token.TokenType == TokenType.Identifier;
        }

        private bool IsStatement(Token token)
        {
            return token.Value == "let" || token.Value == "if" || token.Value == "while" 
                || token.Value == "do" || token.Value == "return";
        }

        private bool HaveExpression(Token nextToken)
        {
            var haveExpression = false;
            switch (nextToken.TokenType)
            {
                case TokenType.IntegerConstant:
                case TokenType.StringConstant:
                case TokenType.Identifier:
                    haveExpression = true;
                    break;
                case TokenType.Keyword:
                    haveExpression = (nextToken.Value == "true" || nextToken.Value == "false"
                        || nextToken.Value == "null" || nextToken.Value == "this");
                    break;
                case TokenType.Symbol:
                    haveExpression = (nextToken.Value == "(" || nextToken.Value == "-" || nextToken.Value == "~");
                    break;
            }
            tokenizer.PushBack(nextToken);

            return haveExpression;
        }
        #endregion
    }
}
