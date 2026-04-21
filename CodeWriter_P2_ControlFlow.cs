using System;

namespace JackCompiling
{
    public partial class CodeWriter
    {
        private static int _labelsCount = 0;
        /// <summary>Statement; Statement; ...</summary>
        public void WriteStatements(StatementsSyntax statements)
        {
            foreach (var statement in statements.Statements)
                WriteStatement(statement);
        }

        private void WriteStatement(StatementSyntax statement)
        {
            var ok = TryWriteVarAssignmentStatement(statement)
                     || TryWriteProgramFlowStatement(statement)
                     || TryWriteDoStatement(statement) // будет реализована в следующий задачах
                     || TryWriteArrayAssignmentStatement(statement)  // будет реализована в следующий задачах
                     || TryWriteReturnStatement(statement);  // будет реализована в следующий задачах
            if (!ok)
                throw new FormatException($"Unknown statement [{statement}]");
        }

        /// <summary>let VarName = Expression;</summary>
        private bool TryWriteVarAssignmentStatement(StatementSyntax statement)
        {
            if (statement is not LetStatementSyntax || ((LetStatementSyntax)statement).Index != null)
                return false;
            var letStatement = (LetStatementSyntax)statement;

            var expression = letStatement.Value;
            WriteExpression(expression);

            var varName = letStatement.VarName;
            var info = FindVarInfo(varName.Value);
            var segmentName = info.SegmentName;
            var segmentIndex = info.Index;

            Write($"pop {segmentName} {segmentIndex}");            

            return true;
        }

        /// <summary>
        /// if ( Expression ) { Statements } [else { Statements }
        /// while ( Expression ) { Statements }
        /// </summary>
        private bool TryWriteProgramFlowStatement(StatementSyntax statement)
        {
            if (statement is IfStatementSyntax)
                WriteIfStatement((IfStatementSyntax)statement);
            else if (statement is WhileStatementSyntax)
                WriteWhileStatement((WhileStatementSyntax)statement);
            else
                return false;

            return true;
        }

        private void WriteIfStatement(IfStatementSyntax statement)
        {
            var label1 = $"if-label-1_{_labelsCount}";
            var label2 = $"if-label-2_{_labelsCount}";
            _labelsCount++;

            var condition = statement.Condition;
            WriteExpression(condition);         

            Write("not");
            Write($"if-goto {label1}");
            WriteStatements(statement.TrueStatements);
            Write($"goto {label2}");
            Write($"label {label1}");
            if (statement.ElseClause != null)
                WriteStatements(statement.ElseClause.FalseStatements);
            Write($"label {label2}");
        }

        private void WriteWhileStatement(WhileStatementSyntax statement)
        {
            var label1 = $"while-label-1_{_labelsCount}";
            var label2 = $"while-label-2_{_labelsCount}";
            _labelsCount++;

            Write($"label {label1}");
            WriteExpression(statement.Condition);
            Write("not");
            Write($"if-goto {label2}");
            WriteStatements(statement.Statements);
            Write($"goto {label1}");
            Write($"label {label2}");
        }
    }
}
