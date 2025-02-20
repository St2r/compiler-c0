using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using compiler_c0.global_config;
using compiler_c0.symbol_manager.instruction;
using compiler_c0.symbol_manager.symbol;
using compiler_c0.symbol_manager.symbol_table;
using ValueType = compiler_c0.symbol_manager.symbol.value_type.ValueType;

namespace compiler_c0.symbol_manager
{
    public class SymbolManager
    {
        // ReSharper disable once InconsistentNaming
        private readonly GlobalConfig GlobalConfig = GlobalConfig.Instance;
        
        private readonly List<SymbolTable> _symbolTables = new();

        private SymbolTable CurSymbolTable => _symbolTables.Last();

        private SymbolTable FunctionSymbolTable => _symbolTables[1];

        public GlobalSymbolTable GlobalSymbolTable => (GlobalSymbolTable) _symbolTables.First();

        private SymbolManager()
        {
            // put a global symbol table to the bottom
            _symbolTables.Add(new GlobalSymbolTable());

            // load standard lib
            LoadLibFunctions();
            
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

        public Variable NewVariable(string name, ValueType type, bool isConst)
        {
            CheckDuplicate(name);
            // alloc offset according to TYPE
            var variable = new Variable(type, isConst);

            CurSymbolTable.AddSymbol(name, variable);
            if (_symbolTables.Count > 1)
            {
                // todo update function symbol table's variable slot;
                // UpdateFunctionVariableSlot();
                CurFunction.LocSlots += 1;
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
            var variable = NewVariable($"fun({name})", ValueType.String, true);
            variable.SetValue(name);

            var function = new Function();

            CurSymbolTable.AddSymbol(name, function);
            CurFunction = function;

            return function;
        }

        public Instruction AddInstruction(Instruction instruction)
        {
            CurFunction.AddInstruction(instruction);
            return instruction;
        }

        public int GetInstructionOffset(Instruction instruction1, Instruction instruction2)
        {
            return CurFunction.GetInstructionOffset(instruction1, instruction2);
        }
        
        public int GetInstructionOffset(Instruction instruction)
        {
            return CurFunction.GetInstructionOffset(instruction);
        }
        
        public void AddLoadAddressInstruction(Symbol symbol)
        {
            if (CurSymbolTable is GlobalSymbolTable)
            {
                var i = GlobalSymbolTable.FindVariable((Variable) symbol);
                if (i == -1)
                    throw new Exception("variable not found");
                CurFunction.AddInstruction(new Instruction(InstructionType.Globa, (uint) i));
            }
            else if (symbol is Variable variable)
            {
                int localOffset = -1;
                var curOffset = 0;
                for (var index = 1;index < _symbolTables.Count;index ++)
                {
                    var f = _symbolTables[index].FindVariable(variable);
                    if (f != -1)
                    {
                        localOffset = f + curOffset;
                        break;
                    }

                    curOffset += _symbolTables[index].VariableCount;
                }
                
                int globalOffset;
                if (localOffset != -1)
                {
                    CurFunction.AddInstruction(new Instruction(InstructionType.Loca, (uint) localOffset));
                }
                else if((globalOffset=GlobalSymbolTable.FindVariable(variable)) != -1)
                {
                    CurFunction.AddInstruction(new Instruction(InstructionType.Globa, (uint) globalOffset));
                }
                else
                {
                    throw new Exception("variable not found");
                }
            }
            else if (symbol is Param param)
            {
                int paramOffset;
                if ((paramOffset = FunctionSymbolTable.FindParam(param)) != -1)
                {
                    CurFunction.AddInstruction(new Instruction(InstructionType.Arga, (uint) paramOffset));
                }
                else
                {
                    throw new Exception("param not found");
                }
            }
        }

        public Param NewParam(string name, ValueType type, bool isConst)
        {
            CheckDuplicate(name);
            var param = new Param(type, isConst);

            CurSymbolTable.AddSymbol(name, param);
            return param;
        }

        public void SetReturnParam(ValueType type)
        {
            CurSymbolTable.SetReturnParam(type);
        }

        public void CreateSymbolTable()
        {
            _symbolTables.Add(new SymbolTable());
        }

        public void DeleteSymbolTable()
        {
            _symbolTables.RemoveAt(_symbolTables.Count - 1);
        }

        public Function CurFunction { get; private set; }

        private void LoadLibFunctions()
        {
            GlobalSymbolTable.NewLibFunction("getint", ValueType.Int);
            GlobalSymbolTable.NewLibFunction("getdouble", ValueType.Float);
            GlobalSymbolTable.NewLibFunction("getchar", ValueType.Int);
            GlobalSymbolTable.NewLibFunction("putint", ValueType.Void, ValueType.Int);
            GlobalSymbolTable.NewLibFunction("putdouble", ValueType.Void, ValueType.Float);
            GlobalSymbolTable.NewLibFunction("putchar", ValueType.Void, ValueType.Int);
            GlobalSymbolTable.NewLibFunction("putstr", ValueType.Void, ValueType.Int);
            GlobalSymbolTable.NewLibFunction("putln", ValueType.Void);
        }

        public void Generator()
        {
            if (_symbolTables.Count != 1 || !(CurSymbolTable is GlobalSymbolTable))
            {
                throw new Exception("symbol manager error with root symbol table");
            }

            // add call main into _start_ function
            var fun = GlobalSymbolTable.FindFunction("_start_", out _);
            var funMain = GlobalSymbolTable.FindFunction("main", out _);
            GlobalSymbolTable.FindVariable("fun(main)", out var offset);
            if (offset == -1)
            {
                throw new Exception("no main function found");
            }
            fun.AddInstruction(new Instruction(InstructionType.StackAlloc, funMain.ReturnSlot));
            fun.AddInstruction(new Instruction(InstructionType.Callname, (uint) offset));
            
            // output binary code
            Console.Write(((GlobalSymbolTable)CurSymbolTable).ToString());
            
            // generate binary code
            using var writer = new BinaryWriter(File.Open(GlobalConfig.OutputFilePath, FileMode.Create));
            writer.Write(GlobalSymbolTable.ToBytes().ToArray());
        }

        public void EnterWhile(Instruction continuePoint) => CurFunction.EnterWhile(continuePoint);

        public void LeaveWhile() => CurFunction.LeaveWhile();
        
        public void SetContinue() => CurFunction.SetContinue();

        public void SetBreak() => CurFunction.SetBreak();
    }
}