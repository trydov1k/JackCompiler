using System;
using System.Collections.Generic;
using System.Linq;

namespace JackCompiling
{
    public partial class CodeWriter
    {
        #region Реализовано
        /// <summary>
        /// class Name { ... }
        /// </summary>
        public void WriteClass(ClassSyntax classSyntax)
        {
            currentClassName = classSyntax.Name.Value;

            var classVars = classSyntax.ClassVars;
            CreateClassSymbolsTable(classVars);

            var subroutines = classSyntax.SubroutineDec;

            foreach (var subroutine in subroutines)
            {
                var subroutineType = subroutine.KindKeyword.Value;
                switch (subroutineType)
                {
                    case "constructor":
                        WriteConstructor(subroutine);
                        break;
                    case "function":
                        WriteFunction(subroutine);
                        break;
                    case "method":
                        WriteMethod(subroutine);
                        break;
                }
            }
        }

        /// <summary>
        /// constructor Type Name ( ParameterList ) { Body }
        /// </summary>
        private void WriteConstructor(SubroutineDecSyntax subroutine)
        {
            var arguments = subroutine.ParameterList.DelimitedParameters;  // Аргументы конструктора

            var dict = CreateMethodSymbolsTableByArguments(arguments);

            var argumentsCount = arguments.Count;

            var subroutineBody = subroutine.SubroutineBody;

            var statementVars = subroutineBody.VarDec;  // Переменные конструктора (var int count;)
            UpdateMethodSymbolsTableByVars(statementVars, dict);
            methodSymbols = dict;

            var varsCount = dict.Values.Count;

            Write($"function {currentClassName}.{subroutine.Name.Value} {varsCount}");

            Write($"push constant {argumentsCount}");  // указываем для скольки полей нам надо найти место в памяти
            Write("call Memory.alloc 1");  // Находим место в памяти (теперь в стеке начальный адрес объекта)
            Write($"pop pointer 0");  // Устанавливаем this = ...

            var statements = subroutineBody.Statements;
            WriteStatements(statements);
        }
        
        /// <summary>
        /// method Type Name ( ParameterList ) { Body }
        /// </summary>
        private void WriteMethod(SubroutineDecSyntax subroutine)
        {
            var arguments = subroutine.ParameterList.DelimitedParameters;  // Аргументы метода
            var newArguments = arguments.Prepend(new Parameter(new Token(TokenType.Keyword, "this", 0, 0), new Token(TokenType.Identifier, "this", 0, 0)));

            var dict = CreateMethodSymbolsTableByArguments(newArguments.ToList());

            var subroutineBody = subroutine.SubroutineBody;

            var statementVars = subroutineBody.VarDec;  // Переменные метода (var int count;)
            UpdateMethodSymbolsTableByVars(statementVars, dict);
            methodSymbols = dict;

            var varsCount = dict.Values.Count;            

            Write($"function {currentClassName}.{subroutine.Name.Value} {varsCount - 1}");

            Write("push argument 0");  // Кладем в стек this (его передали первым параметром)
            Write("pop pointer 0");  // this = argument 0

            var statements = subroutineBody.Statements;
            WriteStatements(statements);

            if (subroutine.ReturnType.Value == "void")
                Write("pop temp 0");
        }
        
        /// <summary>
        /// function Type Name ( ParameterList ) { Body }
        /// </summary>
        private void WriteFunction(SubroutineDecSyntax subroutine)
        {
            var arguments = subroutine.ParameterList.DelimitedParameters;  // Аргументы функции

            var dict = CreateMethodSymbolsTableByArguments(arguments);

            var subroutineBody = subroutine.SubroutineBody;

            var statementVars = subroutineBody.VarDec;  // Переменные функции (var int count;)
            UpdateMethodSymbolsTableByVars(statementVars, dict);
            methodSymbols = dict;

            var varsCount = dict.Values.Count;

            Write($"function {currentClassName}.{subroutine.Name.Value} {varsCount}");

            var statements = subroutineBody.Statements;
            WriteStatements(statements);

            if (subroutine.ReturnType.Value == "void")
                Write("pop temp 0");
        }
        
        /// <summary>
        /// ObjOrClassName . SubroutineName ( ExpressionList ) 
        /// </summary>
        private bool TryWriteSubroutineCall(TermSyntax term)
        {
            if (term is not SubroutineCallTermSyntax)
                return false;

            var subroutineTerm = term as SubroutineCallTermSyntax;

            var call = subroutineTerm.Call;

            WriteCall(call);

            return true;
        }

        private void WriteCall(SubroutineCall call)
        {
            var arguments = call.Arguments.DelimitedExpressions;
            var argumentsCount = arguments.Count;

            var objectOrClass = call.ObjectOrClass;

            if (objectOrClass == null)  // Это метод, который вызвали из другого метода ( myMethod() )
            {
                Write("push pointer 0");
                argumentsCount++;
            }

            var objectOrClassName = objectOrClass == null ? currentClassName : objectOrClass.Name.Value;

            var objectInfo = FindVarInfo(objectOrClassName);

            if (objectInfo != null)  // Это метод ( obj.myMethod() )
            {
                Write($"push {objectInfo.SegmentName} {objectInfo.Index}");  // Пушим this в стек
                argumentsCount++;  // Увеличиваем количество аргуметов, которые мы передадим методу
                objectOrClassName = objectInfo.Type;  // Вызывать будем метод из класса, который является типом объекта
            }

            foreach (var argument in arguments)
                WriteExpression(argument);

            var subroitineName = call.SubroutineName.Value;

            Write($"call {objectOrClassName}.{subroitineName} {argumentsCount}");
        }
        #endregion
        #region Работает 100%
        /// <summary>
        /// do SubroutineCall ; 
        /// </summary>
        private bool TryWriteDoStatement(StatementSyntax statement)
        {
            if (statement is not DoStatementSyntax)
                return false;

            var doStatement = statement as DoStatementSyntax;

            WriteCall(doStatement.SubroutineCall);

            Write("pop temp 0");

            return true;
        }
        
        /// <summary>
        /// return ;
        /// return Expression ;
        /// </summary>
        private bool TryWriteReturnStatement(StatementSyntax statement)
        {
            if (statement is not ReturnStatementSyntax)
                return false;
            var returnStatement = statement as ReturnStatementSyntax;

            var returnValue = returnStatement.ReturnValue;
            WriteExpression(returnValue);
            Write("return");
            return true;
        }
        
        /// <summary>
        /// this | null
        /// </summary>
        private bool TryWriteObjectValue(TermSyntax term)
        {
            var valueTerm = term as ValueTermSyntax;

            if (valueTerm == null || valueTerm.Value.TokenType != TokenType.Keyword 
                || !(valueTerm.Value.Value == "this" || valueTerm.Value.Value == "that"))
                return false;            

            var poinerNumber = valueTerm.Value.Value == "this" ? 0 : 1;

            Write($"push pointer {poinerNumber}");

            return true;
        }
        #endregion
        #region Мои вспомогательные методы
        private void CreateClassSymbolsTable(IReadOnlyList<ClassVarDecSyntax> classVarList)
        {
            var classVarIndex = 0;
            foreach (var classVar in classVarList)
            {
                var varKindString = classVar.KindKeyword.Value;
                var varKind = varKindString == "field" ? VarKind.Field
                    : varKindString == "static" ? VarKind.Static
                    : throw new ArgumentException($"Это должна быть переменная вида field или static, а было {varKindString}");

                foreach (var classVarName in classVar.DelimitedNames)
                {
                    var varInfo = new VarInfo(classVarIndex, varKind, classVar.Type.Value);
                    classVarIndex++;
                    classSymbols[classVarName.Value] = varInfo;
                }
            }
        }

        private Dictionary<string, VarInfo> CreateMethodSymbolsTableByArguments(IReadOnlyList<Parameter> parameters)
        {
            var dict = new Dictionary<string, VarInfo>();
            var parameterIndex = 0;
            foreach (var parameter in parameters)
            {
                var parameterName = parameter.Name.Value;
                dict[parameterName] = new VarInfo(parameterIndex, VarKind.Parameter, parameter.Type.Value);
                parameterIndex++;
            }

            return dict;
        }

        private void UpdateMethodSymbolsTableByVars(IReadOnlyList<VarDecSyntax> vars, Dictionary<string, VarInfo> dict)
        {
            var varIndex = 0;
            foreach (var var in vars)
            {
                foreach (var varName in var.DelimitedNames)
                {
                    dict[varName.Value] = new VarInfo(varIndex, VarKind.Local, var.Type.Value);
                    varIndex++;
                }
            }
        }
        #endregion
    }
}
