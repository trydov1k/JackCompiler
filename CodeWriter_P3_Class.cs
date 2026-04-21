using System;
using System.Collections.Generic;
using System.Linq;

namespace JackCompiling
{
    public partial class CodeWriter
    {
        #region Реализовано 100%
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

            Write($"function {currentClassName}.{subroutine.Name.Value} {arguments.Count}");

            var dict = CreateMethodSymbolsTableByArguments(arguments);

            var argumentsCount = arguments.Count;

            Write($"push constant {argumentsCount}");  // указываем для скольки полей нам надо найти место в памяти
            Write("call Memory.alloc 1");  // Находим место в памяти (теперь в стеке начальный адрес объекта)
            Write($"pop pointer 0");  // Устанавливаем this = ...

            var subroutineBody = subroutine.SubroutineBody;

            var statementVars = subroutineBody.VarDec;  // Переменные конструктора (var int count;)
            UpdateMethodSymbolsTableByVars(statementVars, dict);
            methodSymbols = dict;

            var statements = subroutineBody.Statements;
            WriteStatements(statements);
        }
        
        /// <summary>
        /// method Type Name ( ParameterList ) { Body }
        /// </summary>
        private void WriteMethod(SubroutineDecSyntax subroutine)
        {
            var arguments = subroutine.ParameterList.DelimitedParameters;  // Аргументы метода

            Write($"function {currentClassName}.{subroutine.Name.Value} {arguments.Count + 1}");

            var dict = CreateMethodSymbolsTableByArguments(arguments);

            Write("push argument 0");  // Кладем в стек this (его передали первым параметром)
            Write("pop pointer 0");  // this = argument 0

            var subroutineBody = subroutine.SubroutineBody;

            var statementVars = subroutineBody.VarDec;  // Переменные метода (var int count;)
            UpdateMethodSymbolsTableByVars(statementVars, dict);
            methodSymbols = dict;

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

            Write($"function {currentClassName}.{subroutine.Name.Value} {arguments.Count}");

            var dict = CreateMethodSymbolsTableByArguments(arguments);

            var subroutineBody = subroutine.SubroutineBody;

            var statementVars = subroutineBody.VarDec;  // Переменные функции (var int count;)
            UpdateMethodSymbolsTableByVars(statementVars, dict);
            methodSymbols = dict;

            var statements = subroutineBody.Statements;
            WriteStatements(statements);

            if (subroutine.ReturnType.Value == "void")
                Write("pop temp 0");
        }
        #endregion
        /// <summary>
        /// ObjOrClassName . SubroutineName ( ExpressionList ) 
        /// </summary>
        private bool TryWriteSubroutineCall(TermSyntax term)
        {
            if (term is not SubroutineCallTermSyntax)
                return false;
            var trm = term as SubroutineCallTermSyntax;
            var call = trm.Call;
            var arguments = call.Arguments.DelimitedExpressions;
            var argumentsCount = arguments.Count;
            foreach (var arg in arguments)
                WriteExpression(arg);

            var objectOrClassName = call.ObjectOrClass.Name.Value;


            if (FindVarInfo(objectOrClassName) != null)  // Если был вызван метод, а не конструктор или функция
            {
                Write("push pointer 0");
                argumentsCount++;
            }

            var subroutineName = call.SubroutineName.Value;            

            Write($"call {objectOrClassName}.{subroutineName} {argumentsCount}");

            return true;
        }
        #region временно спрятать
        /// <summary>
        /// do SubroutineCall ; 
        /// </summary>
        private bool TryWriteDoStatement(StatementSyntax statement)
        {
            if (statement is not DoStatementSyntax)
                return false;

            var trm = statement as DoStatementSyntax;

            var call = trm.SubroutineCall;

            var arguments = call.Arguments.DelimitedExpressions;
            var argumentsCount = arguments.Count;
            foreach (var arg in arguments)
                WriteExpression(arg);

            var objectOrClassName = call.ObjectOrClass.Name.Value;

            var subroutineName = call.SubroutineName.Value;

            Write($"call {objectOrClassName}.{subroutineName} {argumentsCount}");
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
            return false;
        }
        #endregion

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
    }
}
