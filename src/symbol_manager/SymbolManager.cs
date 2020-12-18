using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using compiler_c0.instruction;
using compiler_c0.symbol_manager.symbol;
using ValueType = compiler_c0.symbol_manager.value_type.ValueType;

namespace compiler_c0.symbol_manager
{
    public class SymbolManager
    {
        private readonly List<SymbolTable> _symbolTables = new();

        private SymbolTable CurSymbolTable => _symbolTables.Last();

        private GlobalSymbolTable GlobalSymbolTable => (GlobalSymbolTable) _symbolTables.First();

        private SymbolManager()
        {
            // put a global symbol table to the bottom
            _symbolTables.Add(new GlobalSymbolTable());

            // initial _start_ function
            NewFunction("_start_");
        }

        private static SymbolManager _instance;

        public static SymbolManager Instance
        {
            get { return _instance ??= new SymbolManager(); }
        }

        public Symbol FindSymbol(string name)
        {
            for (var i = _symbolTables.Count -1; i >= 0; i--)
            {
                if (_symbolTables[i].FindSymbol(name) != null)
                {
                    return _symbolTables[i].FindSymbol(name);
                }
            }

            return null;
        }

        private void CheckDuplicate(string name)
        {
            if (CurSymbolTable.FindSymbol(name) != null)
            {
                throw new Exception($"duplicated definition: {name}");
            }
        }

        public Variable NewVariable(string name, ValueType type)
        {
            CheckDuplicate(name);
            // alloc offset according to TYPE
            var variable = new Variable(type, true);

            CurSymbolTable.AddSymbol(name, variable);
            if (CurSymbolTable != GlobalSymbolTable)
            {
                CurFunction.AddVariable(variable);
            }

            return variable;
        }

        public Function NewFunction(string name)
        {
            if (!(CurSymbolTable is GlobalSymbolTable))
            {
                throw new Exception("function is not allowed defining here");
            }

            CheckDuplicate(name);
            
            // add FuncName into global variable table
            // FuncName will be linked when creating function
            var variable = NewVariable($"fun({name})", ValueType.String);
            variable.SetValue(name);

            var function = new Function();

            CurSymbolTable.AddSymbol(name, function);
            CurFunction = function;

            return function;
        }

        public void AddInstruction(Instruction instruction)
        {
            CurFunction.AddInstruction(instruction);
        }

        public void AddLoadAddressInstruction(Variable variable)
        {
            if (CurSymbolTable is GlobalSymbolTable)
            {
                var i = GlobalSymbolTable.FindVariable(variable);
                if (i == -1)
                    throw new Exception("variable not found");
                CurFunction.AddInstruction(new Instruction(InstructionType.Globa, (uint) i));
            }
            else
            {
                // todo
                // priority: loc > param > global;
                int i;
                if ((i = CurFunction.FindVariable(variable)) != -1)
                {
                    CurFunction.AddInstruction(new Instruction(InstructionType.Loca, (uint) i));
                }
                else if((i=GlobalSymbolTable.FindVariable(variable)) != -1)
                {
                    CurFunction.AddInstruction(new Instruction(InstructionType.Globa, (uint) i));
                }
                else
                {
                    throw new Exception("variable not found");
                }
            }
        }

        public Param NewParam(string name)
        {
            CheckDuplicate(name);
            var param = new Param();

            CurSymbolTable.AddSymbol(name, param);
            return param;
        }

        public void CreateSymbolTable()
        {
            _symbolTables.Add(new SymbolTable());
        }

        public void DeleteSymbolTable()
        {
            _symbolTables.RemoveAt(_symbolTables.Count - 1);
        }

        public Function CurFunction { get; set; }

        public void Generator()
        {
            if (_symbolTables.Count != 1 || !(CurSymbolTable is GlobalSymbolTable))
            {
                throw new Exception("symbol manager error with root symbol table");
            }

            // check main()
            if (CurSymbolTable is GlobalSymbolTable globalSymbolTable
                && globalSymbolTable.FindFunction("main", out _) == null)
            {
                throw new Exception("no main function found");
            }
            
            // add call main into _start_ function
            var fun = GlobalSymbolTable.FindFunction("_start_", out _);
            var funMain = GlobalSymbolTable.FindFunction("main", out _);
            GlobalSymbolTable.FindVariable("fun(main)", out var offset);
            fun.AddInstruction(new Instruction(InstructionType.StackAlloc, funMain.returnSlot));
            fun.AddInstruction(new Instruction(InstructionType.Callname, (uint) offset));
            
            // output binary code
            Console.Write(((GlobalSymbolTable)CurSymbolTable).ToString());
            
            // todo generate binary code
        }
    }
}