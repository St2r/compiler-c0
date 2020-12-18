using System;
using compiler_c0.instruction;
using compiler_c0.symbol_manager;
using compiler_c0.tokenizer;
using compiler_c0.tokenizer.token;
using compiler_c0.tokenizer.token.extensions;
using ValueType = compiler_c0.symbol_manager.value_type.ValueType;

namespace compiler_c0.analyser.sub_function.expression
{
    public static class ExpressionAnalyser
    {
        private static readonly SymbolManager SymbolManager = SymbolManager.Instance;
        public static ExpressionValue AnalyseExpression()
        {
            var lValue = ParsePrimary();
            return ParseOpg(lValue, 0);
        }

        private static readonly Tokenizer Tokenizer = Tokenizer.Instance;

        private static ExpressionValue ParsePrimary()
        {
            ExpressionValue value = null;
            if (Tokenizer.PeekToken().Is(TokenType.Minus))
            {
                value = AnalyseNegExpression();
            }
            else if (Tokenizer.PeekToken().Is(TokenType.LParen))
            {
                Tokenizer.ExpectToken(TokenType.LParen);
                value = ParsePrimary();
                Tokenizer.ExpectToken(TokenType.LParen);
            }
            else if (Tokenizer.PeekToken().Is(TokenType.Identifier))
            {
                // todo
                AnalyseIdentExpression();
                // AnalyseCallExpression();
            }
            else if (Tokenizer.PeekToken().IsLiteral())
            {
                value = AnalyseLiteralExpression();
            }
            else
            {
                throw new Exception("unreachable code");
            }

            return value;
        }

        private static ExpressionValue ParseOpg(ExpressionValue lValue, int priority)
        {
            while (Tokenizer.PeekToken().IsBinaryOperator() && Tokenizer.PeekToken().GetPriority() >= priority)
            {
                var op = Tokenizer.NextToken();
                var rValue = ParsePrimary();
                while (Tokenizer.PeekToken().IsBinaryOperator() &&
                       (Tokenizer.PeekToken().GetPriority() > op.GetPriority() ||
                        Tokenizer.PeekToken().IsRightAssoc() &&
                        Tokenizer.PeekToken().GetPriority() == op.GetPriority()))
                {
                    lValue = ParseOpg(lValue, Tokenizer.PeekToken().GetPriority());
                }

                lValue = ExpressionCombiner.Combine(lValue, op, rValue);
            }

            return lValue;
        }

        public static ExpressionValue AnalyseNegExpression()
        {
            Tokenizer.ExpectToken(TokenType.Minus);
            var value =  ParsePrimary();
            switch (value.ValueType)
            {
                case ValueType.Int:
                    SymbolManager.AddInstruction(new Instruction(InstructionType.NegI));
                    break;
                case ValueType.Float:
                    SymbolManager.AddInstruction(new Instruction(InstructionType.NegF));
                    break;
            }
            
            return value;
        }

        public static ExpressionValue AnalyseIdentExpression()
        {
            var ident = Tokenizer.ExpectToken(TokenType.Identifier);

            return new ExpressionValue(ValueType.Void);
        }

        public static ExpressionValue AnalyseCallExpression()
        {
            var func = Tokenizer.ExpectToken(TokenType.Identifier);
            Tokenizer.ExpectToken(TokenType.LParen);
            AnalyseCallParamList();
            Tokenizer.ExpectToken(TokenType.RParen);
            return new ExpressionValue(ValueType.Void);
        }

        private static void AnalyseCallParamList()
        {
            Tokenizer.ExpectToken(TokenType.Identifier);
            while (Tokenizer.PeekToken().Is(TokenType.Comma))
            {
                Tokenizer.ExpectToken(TokenType.Comma);
                Tokenizer.ExpectToken(TokenType.Identifier);
            }
        }

        private static ExpressionValue AnalyseLiteralExpression()
        {
            var token = Tokenizer.NextToken();
            switch (token.TokenType)
            {
                case TokenType.LiteralNumber:
                    SymbolManager.CurFunction.AddInstruction(
                        new Instruction(InstructionType.Push, (ulong) token.Value));
                    return new ExpressionValue(ValueType.Int);
                // todo other literal type
            }

            throw new NotImplementedException();
        }
    }
}