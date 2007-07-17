using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;

namespace CellDotNet
{
	/// <summary>
	/// The progressive states that <see cref="MethodCompiler"/> goes through.
	/// </summary>
	internal enum MethodCompileState
	{
		S0None,
		/// <summary>
		/// Before any real processing has been performed.
		/// </summary>
		S1Initial,
		S2TreeConstructionDone,
		/// <summary>
		/// Determine escaping arguments and variables.
		/// </summary>
		S3InstructionSelectionPreparationsDone,
		S4InstructionSelectionDone,
		S5RegisterAllocationDone,
		S6PrologAndEpilogDone,
		/// <summary>
		/// At this point the only changes that must be done to the code
		/// is address changes.
		/// </summary>
		S7BranchesFixed,
		S8AdressSubstitutionDone,
		S9Complete
	}

	/// <summary>
	/// Data used during compilation of a method.
	/// </summary>
	internal class MethodCompiler
	{
		private MethodCompileState _state;

		public MethodCompileState State
		{
			get { return _state; }
			private set { _state = value; }
		}

		private List<BasicBlock> _blocks = new List<BasicBlock>();

		public List<BasicBlock> Blocks
		{
			get { return _blocks; }
		}

		private MethodBase _methodBase;

		public MethodBase MethodBase
		{
			get { return _methodBase; }
		}

		private ReadOnlyCollection<MethodParameter> _parameters;
		public ReadOnlyCollection<MethodParameter> Parameters
		{
			get { return _parameters; }
		}

		private ReadOnlyCollection<MethodVariable> _variables;
		/// <summary>
		/// Local variables.
		/// </summary>
		public ReadOnlyCollection<MethodVariable> Variables
		{
			get { return _variables; }
		}

		public MethodCompiler(MethodBase method)
		{
			_methodBase = method;
			State = MethodCompileState.S1Initial;

			PerformIRTreeConstruction();
			DeriveTypes();
		}

		private void VisitTreeInstructions(Action<TreeInstruction> action)
		{
			foreach (BasicBlock block in _blocks)
			{
				foreach (TreeInstruction root in block.Roots)
				{
					foreach (TreeInstruction inst in root.IterateSubtree())
					{
						action(inst);
					}
				}
			}
		}

		private void AssertState(MethodCompileState requiredState)
		{
			if (State != requiredState)
				throw new InvalidOperationException(string.Format("Operation is invalid for the current state. " +
					"Current state: {0}; required state: {1}.", State, requiredState));
		}

		#region IR tree construction

		private void PerformIRTreeConstruction()
		{
			AssertState(MethodCompileState.S1Initial);

			// Build Variables and Parameters.
			List<MethodParameter> parlist = new List<MethodParameter>();
			int i = 0;
			foreach (ParameterInfo pi in _methodBase.GetParameters())
			{
				Utilities.Assert(pi.Position == i, "pi.Index == i");
				i++;
					
				parlist.Add(new MethodParameter(pi));
			}
			_parameters = new ReadOnlyCollection<MethodParameter>(parlist);

			List<MethodVariable> varlist = new List<MethodVariable>();
			i = 0;
			foreach (LocalVariableInfo lv in _methodBase.GetMethodBody().LocalVariables)
			{
				Utilities.Assert(lv.LocalIndex == i, "lv.LocalIndex == i");
				i++;

				varlist.Add(new MethodVariable(lv));
			}
			_variables = new ReadOnlyCollection<MethodVariable>(varlist);


			ILReader reader;
			BuildBasicBlocks(MethodBase, out reader);
			CheckTreeInstructionCount(reader.InstructionsRead);

			State = MethodCompileState.S2TreeConstructionDone;
		}

		private void BuildBasicBlocks(MethodBase method, out ILReader reader)
		{
			Utilities.Assert(_variables != null, "_variables != null");
			Utilities.Assert(_parameters != null, "_parameters != null");

			BasicBlock currblock = new BasicBlock();
			List<TreeInstruction> stack = new List<TreeInstruction>();
			List<TreeInstruction> branches = new List<TreeInstruction>();

			reader = new ILReader(method);

			TreeInstruction treeinst = null;

			while (reader.Read())
			{
				if (treeinst != null && stack.Count == 0)
				{
					// It is a root exactly when the stack is empty.
					currblock.Roots.Add(treeinst);
				}

				PopBehavior popbehavior = GetPopBehavior(reader.OpCode);
				int pushcount = GetPushCount(reader.OpCode);

				treeinst = new TreeInstruction();
				treeinst.Opcode = reader.OpCode;
				treeinst.Offset = reader.Offset;

				// Replace variable and parametere references with our own types.
				// Do not determine escapes here, since it may change later.
				if (treeinst.Opcode.IRCode == IRCode.Ldloc || treeinst.Opcode.IRCode == IRCode.Stloc ||
					treeinst.Opcode.IRCode == IRCode.Ldloca)
				{
					LocalVariableInfo lvi = (LocalVariableInfo) reader.Operand;
					treeinst.Operand = _variables[lvi.LocalIndex];
				}
				else if (treeinst.Opcode.IRCode == IRCode.Ldarg || treeinst.Opcode.IRCode == IRCode.Starg ||
					treeinst.Opcode.IRCode == IRCode.Ldarga)
				{
					ParameterInfo pi = (ParameterInfo)reader.Operand;
					treeinst.Operand = _parameters[pi.Position];
				}
				else
					treeinst.Operand = reader.Operand;

				// Pop
				switch (popbehavior)
				{
					case PopBehavior.Pop1:
						treeinst.Left = stack[stack.Count - 1];
						stack.RemoveRange(stack.Count - 1, 1);
						break;
					case PopBehavior.Pop2:
						treeinst.Left = stack[stack.Count - 2];
						treeinst.Right = stack[stack.Count - 1];
						stack.RemoveRange(stack.Count - 2, 2);
						break;
					case PopBehavior.PopAll: // "leave"
						throw new NotImplementedException("PopAll");
					case PopBehavior.VarPop: // "ret"
						if (reader.OpCode == IROpCodes.Ret)
						{
							// CLI: "The 10 evaluation stack for the current method must be empty except for the value to be returned."
							if (stack.Count > 0)
							{
								treeinst.Left = stack[0];
								if (stack.Count > 1)
									throw new ILException("Stack.Count > 1 ??");
								stack.Clear();
							}
						}
						else if (reader.OpCode.FlowControl == FlowControl.Call)
						{
							// Build a method call from the stack.
							MethodBase mr = (MethodBase) reader.Operand;
							if (stack.Count < mr.GetParameters().Length)
								throw new ILException("Too few parameters on stack.");

							int hasThisExtraParam = ((int) (mr.CallingConvention & CallingConventions.HasThis) != 0 && 
								reader.OpCode != IROpCodes.Newobj) ? 1 : 0;
							int paramcount = mr.GetParameters().Length + hasThisExtraParam;

							MethodCallInstruction mci = new MethodCallInstruction(mr, reader.OpCode);
							mci.Offset = reader.Offset;
							for (int i = 0; i < paramcount; i++)
							{
								mci.Parameters.Add(stack[stack.Count - paramcount + i]);
							}
							stack.RemoveRange(stack.Count - paramcount, paramcount);

							MethodInfo methodinfo = mr as MethodInfo;
							if (mr is ConstructorInfo || (methodinfo != null && methodinfo.ReturnType != typeof(void)))
								pushcount = 1;
							else
								pushcount = 0;

							treeinst = mci;
						}
						else
							throw new Exception("Unknown VarPop.");
						//							throw new Exception("Method calls are not supported.");
						break;
					case PopBehavior.Pop3:
						if (reader.OpCode.StackBehaviourPush != StackBehaviour.Push0)
							throw new ILException("Pop3 with a push != 0?");
						throw new NotImplementedException();
					default:
						if (popbehavior != PopBehavior.Pop0)
							throw new Exception("Invalid PopBehavior: " + popbehavior + ". Only two-argument method calls are supported.");
						break;
				}

				// Push
				if (pushcount == 1)
					stack.Add(treeinst);
				else if (pushcount != 0)
					throw new Exception("Only 1-push is supported.");

				switch (reader.OpCode.FlowControl)
				{
					case FlowControl.Branch:
					case FlowControl.Cond_Branch:
					case FlowControl.Return:
					case FlowControl.Throw:
						if (reader.OpCode.FlowControl == FlowControl.Branch ||
						    reader.OpCode.FlowControl == FlowControl.Cond_Branch)
						{
							// For now, just store the target offset; this is fixed below.

							treeinst.Operand = reader.Operand;
							branches.Add(treeinst);
						}
						break;
					case FlowControl.Call:
					case FlowControl.Meta:
					case FlowControl.Next:
//					case FlowControl.Phi:
					case FlowControl.Break:
						break;
					default:
						throw new ILException();
				}
			}

			//Adds the last instruction tree and basic Block
			if (treeinst != null)
			{
				currblock.Roots.Add(treeinst);
				Blocks.Add(currblock);
			}

			// Fix branches.
			// It is by definition only possible to branch to basic blocks.
			// So we need to create these blocks.
			Dictionary<int, BasicBlock> basicBlockOffsets = new Dictionary<int, BasicBlock>();

			foreach (TreeInstruction branchinst in branches)
			{
				int targetPos = (int) branchinst.Operand;
				BasicBlock target;

				if (basicBlockOffsets.TryGetValue(targetPos, out target))
				{
					branchinst.Operand = target;
					continue;
				}

				// Find root to create basic block from.
				for (int bbindex = 0; bbindex < Blocks.Count; bbindex++)
				{
					BasicBlock bb = Blocks[bbindex];
					for (int rootindex = 0; rootindex < bb.Roots.Count; rootindex++)
					{
						TreeInstruction firstinst = bb.Roots[rootindex].GetFirstInstruction();

						if (firstinst.Offset != targetPos)
							continue;

						// Need to create new bb from this root.
						List<TreeInstruction> newblockroots = bb.Roots.GetRange(rootindex, bb.Roots.Count - rootindex);
						bb.Roots.RemoveRange(rootindex, bb.Roots.Count - rootindex);

						BasicBlock newbb = new BasicBlock();
						newbb.Roots.AddRange(newblockroots);
						Blocks.Insert(bbindex + 1, newbb);

						basicBlockOffsets.Add(firstinst.Offset, newbb);
						target = newbb;
						goto NextBranch;
					}
				}
				throw new Exception("IR tree construction error. Can't find branch target " + targetPos.ToString("x4") + " from instruction at " + branchinst.Offset.ToString("x4") + ".");

			NextBranch:
				branchinst.Operand = target;
			}

			Console.WriteLine("after parse:");
			new TreeDrawer().DrawMethod(this, method);

		}

		/// <summary>
		/// Returns the number of values pushed by the opcode. -1 is returned for function calls.
		/// </summary>
		/// <param name="code"></param>
		/// <returns></returns>
		private static int GetPushCount(IROpCode code)
		{
			int pushCount;

			switch (code.StackBehaviourPush)
			{
				case StackBehaviour.Push0:
					pushCount = 0;
					break;
				case StackBehaviour.Push1:
				case StackBehaviour.Pushi:
				case StackBehaviour.Pushi8:
				case StackBehaviour.Pushr4:
				case StackBehaviour.Pushr8:
				case StackBehaviour.Pushref:
					pushCount = 1;
					break;
				case StackBehaviour.Push1_push1:
					pushCount = 2;
					break;
				case StackBehaviour.Varpush:
				default:
					pushCount = -1;
					break;
			}

			return pushCount;
		}

		private enum PopBehavior
		{
			Pop0 = 0,
			Pop1 = 1,
			Pop2 = 2,
			Pop3 = 3,
			PopAll = 1000,
			VarPop = 1001
		}

		private static PopBehavior GetPopBehavior(IROpCode code)
		{
			PopBehavior pb;

			switch (code.StackBehaviourPop)
			{
				case StackBehaviour.Pop0:
					pb = PopBehavior.Pop0;
					break;
				case StackBehaviour.Varpop:
					pb = PopBehavior.VarPop;
					break;
				case StackBehaviour.Pop1:
				case StackBehaviour.Popi:
				case StackBehaviour.Popref:
					pb = PopBehavior.Pop1;
					break;
				case StackBehaviour.Pop1_pop1:
				case StackBehaviour.Popi_pop1:
				case StackBehaviour.Popi_popi:
				case StackBehaviour.Popi_popi8:
				case StackBehaviour.Popi_popr4:
				case StackBehaviour.Popi_popr8:
				case StackBehaviour.Popref_pop1:
				case StackBehaviour.Popref_popi:
					pb = PopBehavior.Pop2;
					break;
				case StackBehaviour.Popi_popi_popi:
				case StackBehaviour.Popref_popi_pop1:
				case StackBehaviour.Popref_popi_popi:
				case StackBehaviour.Popref_popi_popi8:
				case StackBehaviour.Popref_popi_popr4:
				case StackBehaviour.Popref_popi_popr8:
				case StackBehaviour.Popref_popi_popref:
					pb = PopBehavior.Pop3;
					break;
				default:
					throw new ArgumentOutOfRangeException("code");
			}

			return pb;
		}

		/// <summary>
		/// Checks that the number of instructions in the constructed tree is equal to the number of IL instructions in the cecil model.
		/// </summary>
		/// <param name="correctCount"></param>
		private void CheckTreeInstructionCount(int correctCount)
		{
			int count = 0;
			VisitTreeInstructions(delegate
			{
				count += 1;
			});

			if (count != correctCount)
			{
				string msg = string.Format("Invalid tree instruction count of {0}. Should have been {1}.", count, correctCount);
				TreeDrawer td= new TreeDrawer();
				td.DrawMethod(this, MethodBase);
				throw new Exception(msg);
			}
		}

		#endregion

		#region Instruction selection preparations

		private void PerformInstructionSelectionPreparations()
		{
			if (State != MethodCompileState.S2TreeConstructionDone)
				throw new InvalidOperationException("State != MethodCompileState.S2TreeConstructionDone");

			DetermineEscapes();

			State = MethodCompileState.S3InstructionSelectionPreparationsDone;
		}

		/// <summary>
		/// Determines escapes in the tree and allocates virtual registers to them if they haven't
		/// already got them.
		/// </summary>
		private void DetermineEscapes()
		{
			foreach (MethodVariable var in Variables)
			{
				var.Escapes = false;
				if (var.VirtualRegister == null)
					var.VirtualRegister = NextRegister();
			}
			foreach (MethodParameter p in Parameters)
			{
				p.Escapes = false;
				if (p.VirtualRegister == null)
					p.VirtualRegister = NextRegister();
			}

			Action<TreeInstruction> action =
				delegate(TreeInstruction obj)
					{
						if (obj.Opcode.IRCode == IRCode.Ldarga)
							((MethodParameter)obj.Operand).Escapes = true;
						else if (obj.Opcode.IRCode == IRCode.Ldloca)
							((MethodVariable) obj.Operand).Escapes = true;
					};
			VisitTreeInstructions(action);
		}

		#endregion

		private int _virtualRegisterNum = -1000; // Arbitrary...
		private VirtualRegister NextRegister()
		{
			return new VirtualRegister(_virtualRegisterNum++);
		}

		private void PerformInstructionSelection()
		{
			AssertState(MethodCompileState.S3InstructionSelectionPreparationsDone);

			_instructions = new SpuInstructionWriter();

			ILTreeSpuWriter writer = new ILTreeSpuWriter();
			writer.GenerateCode(this, _instructions);

			State = MethodCompileState.S4InstructionSelectionDone;
		}

		/// <summary>
		/// This is only for unit test
		/// </summary>
		/// <returns></returns>
		public SpuInstructionWriter GetSpuInstructionWriter()
		{
			if (State < MethodCompileState.S4InstructionSelectionDone)
				throw new InvalidOperationException("State < MethodCompileState.S4InstructionSelectionDone");

			return _instructions;
		}


		public int GetSpuInstructionCount()
		{
			if (State < MethodCompileState.S4InstructionSelectionDone)
				throw new InvalidOperationException("Too early. State: " + State);

			return _instructions.BasicBlocks.Count;
		}

		/// <summary>
		/// Brings the compiler process up to the specified state.
		/// </summary>
		/// <param name="targetState"></param>
		public void PerformProcessing(MethodCompileState targetState)
		{
			if (State >= targetState)
				return; // Already there...

			if (State < MethodCompileState.S2TreeConstructionDone && targetState >= MethodCompileState.S2TreeConstructionDone)
				PerformIRTreeConstruction();

			if (State < MethodCompileState.S3InstructionSelectionPreparationsDone && targetState >= MethodCompileState.S3InstructionSelectionPreparationsDone)
				PerformInstructionSelectionPreparations();

			if (State < MethodCompileState.S4InstructionSelectionDone && targetState >= MethodCompileState.S4InstructionSelectionDone)
				PerformInstructionSelection();

			if (State < MethodCompileState.S5RegisterAllocationDone && targetState >= MethodCompileState.S5RegisterAllocationDone)
				PerformRegisterAllocation();

			if (State < MethodCompileState.S6PrologAndEpilogDone && targetState >= MethodCompileState.S6PrologAndEpilogDone)
				PerformPrologAndEpilogGeneration();

			if (State < MethodCompileState.S7BranchesFixed && targetState >= MethodCompileState.S7BranchesFixed)
				PerformBranchOffsetDetermination();


			if (targetState >= MethodCompileState.S8AdressSubstitutionDone)
			{
				if (targetState <= MethodCompileState.S9Complete) 
					throw new NotImplementedException("Target state: " + targetState);
				else 
					throw new ArgumentException("Invalid state: " + targetState, "targetState");
			}
		}

		private void PerformRegisterAllocation()
		{
			AssertState(MethodCompileState.S4InstructionSelectionDone);

			SimpleRegAlloc regalloc = new SimpleRegAlloc();
			List<SpuInstruction> asm = _instructions.GetAsList();
			regalloc.alloc(asm, 16);

			State = MethodCompileState.S5RegisterAllocationDone;
		}

		#region Prolog/epilog
		SpuInstructionWriter _prolog;
		SpuInstructionWriter _epilog;

		private void PerformPrologAndEpilogGeneration()
		{
			Utilities.Assert(State == MethodCompileState.S5RegisterAllocationDone, "Invalid state: " + State);

			_prolog = new SpuInstructionWriter();
			_prolog.BeginNewBasicBlock();
			WriteProlog(_prolog);

			_epilog = new SpuInstructionWriter();
			_epilog.BeginNewBasicBlock();
			WriteEpilog(_epilog);

			_state = MethodCompileState.S6PrologAndEpilogDone;
		}

		private VirtualRegister GetHardwareRegister(int hwregnum)
		{
			VirtualRegister reg = new VirtualRegister();
			HardwareRegister hwreg = new HardwareRegister();
			hwreg.Register = hwregnum;
			reg.Location = hwreg;

			return reg;
		}

		private void WriteProlog(SpuInstructionWriter prolog)
		{
			// TODO: Store caller-saves registers that this method uses, based on negative offsts
			// from the caller's SP. Set GRSA_slots.


			// Number of 16 byte slots in the frame.
			int RASA_slots = 0; // Register argument save area. (vararg)
			int GRSA_slots = 0; // General register save area. (non-volatile registers)
			int LVS_slots = 0; // Local variable space. (escapes and spills)
			int PLA_slots = 0; // Parameter list area. (more than 72 argument registers)
			int frameSlots = RASA_slots + GRSA_slots + LVS_slots + PLA_slots + 2;

			// First/topmost caller-saves register caller SP slot offset.
//			int first_GRSA_slot_offset = -(RASA_slots + 1);

			VirtualRegister lr = GetHardwareRegister(0);
			VirtualRegister sp = GetHardwareRegister(1);

			// Save LR in caller's frame.
			prolog.WriteStqd(lr, sp, 1);

			// Establish new SP.
			prolog.WriteSfi(sp, sp, -frameSlots*16);

			// Store SP at new frame's Back Chain.
			prolog.WriteStqd(sp, sp, 0);
		}

		private void WriteEpilog(SpuInstructionWriter epilog)
		{
			// Assume that the code that wants to return has placed the return value in the correct
			// registers (R3+).

			VirtualRegister lr = GetHardwareRegister(0);
			VirtualRegister sp = GetHardwareRegister(1);

			// Restore old SP.
			epilog.WriteLqd(sp, sp, 0);

			// TODO: Restore caller-saves.

			// Restore old LR from callers frame.
			epilog.WriteLqd(lr, sp, 1);

			// Return.
			epilog.WriteBi(lr);
		}

		#endregion

		private void PerformBranchOffsetDetermination()
		{
			AssertState(MethodCompileState.S6PrologAndEpilogDone);

			// Iterate bbs, instructions to determine bb offsets and collect branch instructions,
			// so that the branch instructions afterwards can be patched with the bb addresses.

			Utilities.Assert(_prolog.BasicBlocks.Count == 0, "_prolog.BasicBlocks.Count == 0");
			Utilities.Assert(_epilog.BasicBlocks.Count == 0, "_epilog.BasicBlocks.Count == 0");

			List<SpuBasicBlock> bblist = new List<SpuBasicBlock>();
			bblist.Add(_prolog.BasicBlocks[0]);
			foreach (SpuBasicBlock block in _instructions.BasicBlocks)
				bblist.Add(block);
			bblist.Add(_epilog.BasicBlocks[0]);


			// All offsets are byte offset from start of method (prolog).

			List<KeyValuePair<int, SpuInstruction>> branchlist = new List<KeyValuePair<int, SpuInstruction>>();
			int curroffset = 0;
			foreach (SpuBasicBlock bb in bblist)
			{
				bb.Offset = curroffset;

				SpuInstruction inst = bb.Head;
				while (inst != null)
				{
					if (inst.JumpTarget != null)
						branchlist.Add(new KeyValuePair<int, SpuInstruction>(curroffset, inst));
					else if (inst.OpCode == SpuOpCode.ret)
					{
						inst.OpCode = SpuOpCode.br;
						inst.JumpTarget = _epilog.BasicBlocks[0];
						branchlist.Add(new KeyValuePair<int, SpuInstruction>(curroffset, inst));
					}

					inst = inst.Next;
					curroffset += 4;
				}
			}

			// Insert offsets.
			foreach (KeyValuePair<int, SpuInstruction> branchpair in branchlist)
			{
				SpuBasicBlock targetbb = branchpair.Value.JumpTarget;

				int relativebranchbytes = branchpair.Key - targetbb.Offset;
				branchpair.Value.Constant = relativebranchbytes >> 2;
			}


			State = MethodCompileState.S7BranchesFixed;
		}


		private TypeCache _typecache = new TypeCache();
		private SpuInstructionWriter _instructions;

		/// <summary>
		/// Translates the type reference to a <see cref="TypeDescription"/>.
		/// </summary>
		/// <param name="reference"></param>
		/// <returns></returns>
		private TypeDescription GetTypeDescription(Type reference)
		{
			return _typecache.GetTypeDescription(reference);
		}

		/// <summary>
		/// Creates and caches <see cref="TypeDescription"/> objects.
		/// </summary>
		public class TypeCache
		{
			/// <summary>
			/// Key is assembly name and full type name.
			/// </summary>
			private Dictionary<Type, TypeDescription> _history;

			public TypeCache()
			{
				_history = new Dictionary<Type, TypeDescription>();
			}

			public TypeDescription GetTypeDescription(Type type)
			{
//				if (!type.IsPrimitive)
//					throw new ArgumentException();

				TypeDescription desc;
				if (_history.TryGetValue(type, out desc))
					return desc;

				desc = CreateTypeDescription(type);
				_history.Add(type, desc);

				return desc;
			}

			private TypeDescription CreateTypeDescription(Type type)
			{
				// TODO: This reference stuff is wrong.
				if (type.IsByRef)
					return new TypeDescription(type.MakeByRefType());
				else if (type.IsPointer)
					return new TypeDescription(type.MakePointerType());
				else if (type.IsGenericType)
					throw new NotImplementedException("Generic types are not yet implemented.");
				else
					return new TypeDescription(type);
			}
		}

		#region Type deriving

		/// <summary>
		/// Derives the types of each tree node using a bottom-up analysis.
		/// </summary>
		private void DeriveTypes()
		{
			foreach (BasicBlock block in Blocks)
			{
				foreach (TreeInstruction root in block.Roots)
				{
					DeriveType(root, 0);
				}
			}
		}

		/// <summary>
		/// This is the recursive part of DeriveTypes.
		/// </summary>
		/// <param name="inst"></param>
		/// <param name="level"></param>
		private void DeriveType(TreeInstruction inst, int level)
		{
			foreach (TreeInstruction child in inst.GetChildInstructions())
				DeriveType(child, level + 1);

			TreeInstruction firstchild;
			Utilities.TryGetFirst(inst.GetChildInstructions(), out firstchild);

			StackTypeDescription t;
			switch (inst.Opcode.FlowControl)
			{
				case FlowControl.Branch:
				case FlowControl.Break:
					if (level != 0)
						throw new NotImplementedException("Only root branches are implemented.");
					t = StackTypeDescription.None;
					break;
				case FlowControl.Call:
					{
						MethodCallInstruction mci = (MethodCallInstruction) inst;

						MethodInfo method = mci.Method as MethodInfo; // might be a constructor.
						// TODO: Handle void type.
						if (method != null && method.ReturnType != typeof(void))
						{
							t = GetStackTypeDescription(method.ReturnType);
						}
						else
							t = StackTypeDescription.None;

						foreach (TreeInstruction param in mci.Parameters)
						{
							DeriveType(param, level + 1);
						}
					}
					break;
				case FlowControl.Cond_Branch:
					if (level != 0)
						throw new NotImplementedException("Only root branches are implemented.");
					t = StackTypeDescription.None;
					break;
//				case FlowControl.Meta:
//				case FlowControl.Phi:
//					throw new ILException("Meta or Phi.");
				case FlowControl.Next:
					try
					{
						t = DeriveTypeForFlowNext(inst, level);
					}
					catch (NotImplementedException e)
					{
						throw new NotImplementedException("Error while deriving flow instruction opcode: " + inst.Opcode.IRCode, e);
					}
					break;
				case FlowControl.Return:
					if ( firstchild != null)
						t = inst.Left.StackType;
					else
						t = StackTypeDescription.None;
					break;
				case FlowControl.Throw:
					t = StackTypeDescription.None;
					break;
				default:
					throw new ILException("Invalid FlowControl: " + inst.Opcode.FlowControl);
			}


			inst.StackType = t;
		}

		/// <summary>
		/// Used by <see cref="DeriveType"/> to derive types for instructions 
		/// that do not change the flow; that is, OpCode.Flow == Next.
		/// </summary>
		/// <param name="inst"></param>
		/// <param name="level"></param>
		private StackTypeDescription DeriveTypeForFlowNext(TreeInstruction inst, int level)
		{
			// The cases are generated and all opcodes with flow==next are present 
			// (except macro codes such as ldc.i4.3).
			StackTypeDescription t;

			Type optype;
			if (inst.Operand is Type)
				optype = (Type) inst.Operand;
			else if (inst.Operand is MethodVariable)
				optype = ((MethodVariable)inst.Operand).Type;
			else if (inst.Operand is MethodParameter)
				optype = ((MethodParameter) inst.Operand).Type;
			else
				optype = null;

			switch (inst.Opcode.IRCode)
			{
				case IRCode.Nop: // nop
					t = StackTypeDescription.None;
					break;
				case IRCode.Ldnull: // ldnull
					t = StackTypeDescription.ObjectType;
					break;
				case IRCode.Ldc_I4: // ldc.i4
					t = StackTypeDescription.Int32;
					break;
				case IRCode.Ldc_I8: // ldc.i8
					t = StackTypeDescription.Int64;
					break;
				case IRCode.Ldc_R4: // ldc.r4
					t = StackTypeDescription.Float32;
					break;
				case IRCode.Ldc_R8: // ldc.r8
					t = StackTypeDescription.Float64;
					break;
				case IRCode.Dup: // dup
					throw new NotImplementedException("dup, pop");
				case IRCode.Pop: // pop
					if (level != 0)
						throw new NotImplementedException("Pop only supported at root level.");
					t = StackTypeDescription.None;
					break;
				case IRCode.Ldind_I1: // ldind.i1
					t = StackTypeDescription.Int8;
					break;
				case IRCode.Ldind_U1: // ldind.u1
					t = StackTypeDescription.UInt8;
					break;
				case IRCode.Ldind_I2: // ldind.i2
					t = StackTypeDescription.Int16;
					break;
				case IRCode.Ldind_U2: // ldind.u2
					t = StackTypeDescription.UInt16;
					break;
				case IRCode.Ldind_I4: // ldind.i4
					t = StackTypeDescription.Int32;
					break;
				case IRCode.Ldind_U4: // ldind.u4
					t = StackTypeDescription.UInt32;
					break;
				case IRCode.Ldind_I8: // ldind.i8
					t = StackTypeDescription.Int64;
					break;
				case IRCode.Ldind_I: // ldind.i
					t = StackTypeDescription.NativeInt;
					break;
				case IRCode.Ldind_R4: // ldind.r4
					t = StackTypeDescription.Float32;
					break;
				case IRCode.Ldind_R8: // ldind.r8
					t = StackTypeDescription.Float64;
					break;
				case IRCode.Ldind_Ref: // ldind.ref
					t = StackTypeDescription.ObjectType;
					break;
				case IRCode.Stind_Ref: // stind.ref
				case IRCode.Stind_I1: // stind.i1
				case IRCode.Stind_I2: // stind.i2
				case IRCode.Stind_I4: // stind.i4
				case IRCode.Stind_I8: // stind.i8
				case IRCode.Stind_R4: // stind.r4
				case IRCode.Stind_R8: // stind.r8
					if (level != 0)
						throw new NotSupportedException();
					t = StackTypeDescription.None;
					break;
				case IRCode.Add: // add
				case IRCode.Div: // div
				case IRCode.Sub: // sub
				case IRCode.Mul: // mul
				case IRCode.Rem: // rem

				case IRCode.Add_Ovf: // add.ovf
				case IRCode.Add_Ovf_Un: // add.ovf.un
				case IRCode.Mul_Ovf: // mul.ovf
				case IRCode.Mul_Ovf_Un: // mul.ovf.un
				case IRCode.Sub_Ovf: // sub.ovf
				case IRCode.Sub_Ovf_Un: // sub.ovf.un
					t = GetNumericResultType(inst.Left.StackType, inst.Right.StackType);
					break;
				case IRCode.And: // and
				case IRCode.Div_Un: // div.un
				case IRCode.Not: // not
				case IRCode.Or: // or
				case IRCode.Rem_Un: // rem.un
				case IRCode.Xor: // xor
					// From CIL table 5.
					if (inst.Left.StackType == inst.Right.StackType)
						t = inst.Left.StackType;
					else
					{
						// Must be native (u)int.
						if (inst.Left.StackType.IsSigned)
							t = StackTypeDescription.NativeInt;
						else
							t = StackTypeDescription.NativeUInt;
					}
					break;
				case IRCode.Shl: // shl
				case IRCode.Shr: // shr
				case IRCode.Shr_Un: // shr.un
					// CIL table 6.
					t = inst.Left.StackType;
					break;
				case IRCode.Neg: // neg
					t = inst.Left.StackType;
					break;
				case IRCode.Cpobj: // cpobj
				case IRCode.Ldobj: // ldobj
				case IRCode.Ldstr: // ldstr
				case IRCode.Castclass: // castclass
				case IRCode.Isinst: // isinst
				case IRCode.Unbox: // unbox
				case IRCode.Ldfld: // ldfld
				case IRCode.Ldflda: // ldflda
				case IRCode.Stfld: // stfld
				case IRCode.Ldsfld: // ldsfld
				case IRCode.Ldsflda: // ldsflda
				case IRCode.Stsfld: // stsfld
				case IRCode.Stobj: // stobj
					throw new NotImplementedException(inst.Opcode.IRCode.ToString());
				case IRCode.Conv_Ovf_I8_Un: // conv.ovf.i8.un
				case IRCode.Conv_I8: // conv.i8
				case IRCode.Conv_Ovf_I8: // conv.ovf.i8
					t = StackTypeDescription.Int64;
					break;
				case IRCode.Conv_R4: // conv.r4
					t = StackTypeDescription.Float32;
					break;
				case IRCode.Conv_R8: // conv.r8
					t = StackTypeDescription.Float64;
					break;
				case IRCode.Conv_I1: // conv.i1
				case IRCode.Conv_Ovf_I1_Un: // conv.ovf.i1.un
				case IRCode.Conv_Ovf_I1: // conv.ovf.i1
					t = StackTypeDescription.Int8;
					break;
				case IRCode.Conv_I2: // conv.i2
				case IRCode.Conv_Ovf_I2: // conv.ovf.i2
				case IRCode.Conv_Ovf_I2_Un: // conv.ovf.i2.un
					t = StackTypeDescription.Int16;
					break;
				case IRCode.Conv_Ovf_I4: // conv.ovf.i4
				case IRCode.Conv_I4: // conv.i4
				case IRCode.Conv_Ovf_I4_Un: // conv.ovf.i4.un
					t = StackTypeDescription.Int32;
					break;
				case IRCode.Conv_U4: // conv.u4
				case IRCode.Conv_Ovf_U4: // conv.ovf.u4
				case IRCode.Conv_Ovf_U4_Un: // conv.ovf.u4.un
					t = StackTypeDescription.UInt32;
					break;
				case IRCode.Conv_R_Un: // conv.r.un
					t = StackTypeDescription.Float32; // really F, but we're 32 bit.
					break;
				case IRCode.Conv_Ovf_U1_Un: // conv.ovf.u1.un
				case IRCode.Conv_U1: // conv.u1
				case IRCode.Conv_Ovf_U1: // conv.ovf.u1
					t = StackTypeDescription.UInt8;
					break;
				case IRCode.Conv_Ovf_U2_Un: // conv.ovf.u2.un
				case IRCode.Conv_U2: // conv.u2
				case IRCode.Conv_Ovf_U2: // conv.ovf.u2
					t = StackTypeDescription.UInt16;
					break;
				case IRCode.Conv_U8: // conv.u8
				case IRCode.Conv_Ovf_U8_Un: // conv.ovf.u8.un
				case IRCode.Conv_Ovf_U8: // conv.ovf.u8
					t = StackTypeDescription.UInt64;
					break;
				case IRCode.Conv_I: // conv.i
				case IRCode.Conv_Ovf_I: // conv.ovf.i
				case IRCode.Conv_Ovf_I_Un: // conv.ovf.i.un
					t = StackTypeDescription.NativeInt;
					break;
				case IRCode.Conv_Ovf_U_Un: // conv.ovf.u.un
				case IRCode.Conv_Ovf_U: // conv.ovf.u
				case IRCode.Conv_U: // conv.u
					t = StackTypeDescription.NativeUInt;
					break;
				case IRCode.Box: // box
					throw new NotImplementedException();
				case IRCode.Newarr: // newarr
//					t = GetStackTypeDescription(optype);
//					t = new StackTypeDescription(new TypeDescription());
					throw new NotImplementedException();
				case IRCode.Ldlen: // ldlen
					t = StackTypeDescription.NativeUInt;
					break;
				case IRCode.Ldelema: // ldelema
					throw new NotImplementedException();
				case IRCode.Ldelem_I1: // ldelem.i1
					t = StackTypeDescription.Int8;
					break;
				case IRCode.Ldelem_U1: // ldelem.u1
					t = StackTypeDescription.UInt8;
					break;
				case IRCode.Ldelem_I2: // ldelem.i2
					t = StackTypeDescription.Int32;
					break;
				case IRCode.Ldelem_U2: // ldelem.u2
					t = StackTypeDescription.UInt16;
					break;
				case IRCode.Ldelem_I4: // ldelem.i4
					t = StackTypeDescription.Int32;
					break;
				case IRCode.Ldelem_U4: // ldelem.u4
					t = StackTypeDescription.UInt32;
					break;
				case IRCode.Ldelem_I8: // ldelem.i8
					t = StackTypeDescription.Int64;
					break;
				case IRCode.Ldelem_I: // ldelem.i
					// Guess this can also be unsigned?
					t = StackTypeDescription.NativeInt;
					break;
				case IRCode.Ldelem_R4: // ldelem.r4
					t = StackTypeDescription.Float32;
					break;
				case IRCode.Ldelem_R8: // ldelem.r8
					t = StackTypeDescription.Float64;
					break;
				case IRCode.Ldelem_Ref: // ldelem.ref
					throw new NotImplementedException();
				case IRCode.Stelem_I: // stelem.i
				case IRCode.Stelem_I1: // stelem.i1
				case IRCode.Stelem_I2: // stelem.i2
				case IRCode.Stelem_I4: // stelem.i4
				case IRCode.Stelem_I8: // stelem.i8
				case IRCode.Stelem_R4: // stelem.r4
				case IRCode.Stelem_R8: // stelem.r8
				case IRCode.Stelem_Ref: // stelem.ref
					t = StackTypeDescription.None;
					break;
//				case IRCode.Ldelem_Any: // ldelem.any
//				case IRCode.Stelem_Any: // stelem.any
//					throw new ILException("ldelem_any and stelem_any are invalid.");
				case IRCode.Unbox_Any: // unbox.any
				case IRCode.Refanyval: // refanyval
				case IRCode.Ckfinite: // ckfinite
				case IRCode.Mkrefany: // mkrefany
				case IRCode.Ldtoken: // ldtoken
				case IRCode.Stind_I: // stind.i
				case IRCode.Arglist: // arglist
					throw new NotImplementedException();
				case IRCode.Ceq: // ceq
				case IRCode.Cgt: // cgt
				case IRCode.Cgt_Un: // cgt.un
				case IRCode.Clt: // clt
				case IRCode.Clt_Un: // clt.un
					t = StackTypeDescription.Int8; // CLI says int32, but let's try...
					break;
				case IRCode.Ldftn: // ldftn
				case IRCode.Ldvirtftn: // ldvirtftn
					throw new NotImplementedException();
				case IRCode.Ldarg: // ldarg
				case IRCode.Ldloca: // ldloca
				case IRCode.Ldloc: // ldloc
				case IRCode.Ldarga: // ldarga
					t = GetStackTypeDescription(optype);
					if (t == StackTypeDescription.None)
						throw new NotImplementedException("Only numeric CIL types are implemented.");
					if (inst.Opcode.IRCode == IRCode.Ldloca || inst.Opcode.IRCode == IRCode.Ldarga)
						t = t.GetManagedPointer();

					break;
				case IRCode.Starg: // starg
				case IRCode.Stloc: // stloc
					t = StackTypeDescription.None;
					break;
				case IRCode.Localloc: // localloc
					throw new NotImplementedException();
				case IRCode.Initobj: // initobj
				case IRCode.Constrained: // constrained.
				case IRCode.Cpblk: // cpblk
				case IRCode.Initblk: // initblk
//				case IRCode.No: // no.
				case IRCode.Sizeof: // sizeof
				case IRCode.Refanytype: // refanytype
				case IRCode.Readonly: // readonly.
					throw new NotImplementedException();
				default:
					throw new ILException();
			}

			return t;
		}

		private static Dictionary<Type, StackTypeDescription> s_metadataCilTypes = BuildBasicMetadataCilDictionary();
		private static Dictionary<Type, StackTypeDescription> BuildBasicMetadataCilDictionary()
		{
			Dictionary<Type, StackTypeDescription> dict = new Dictionary<Type, StackTypeDescription>();

			// TODO: the typeof() token values are not what cecil returns...
			dict.Add(typeof(bool), StackTypeDescription.Int8); // Correct?
			dict.Add(typeof(sbyte), StackTypeDescription.Int8);
			dict.Add(typeof(byte), StackTypeDescription.UInt8);
			dict.Add(typeof(short), StackTypeDescription.Int16);
			dict.Add(typeof(ushort), StackTypeDescription.UInt16);
			dict.Add(typeof(char), StackTypeDescription.UInt16); // Correct?
			dict.Add(typeof(int), StackTypeDescription.Int32);
			dict.Add(typeof(uint), StackTypeDescription.UInt32);
			dict.Add(typeof(long), StackTypeDescription.Int64);
			dict.Add(typeof(ulong), StackTypeDescription.UInt64);
			dict.Add(typeof(IntPtr), StackTypeDescription.NativeInt);
			dict.Add(typeof(UIntPtr), StackTypeDescription.NativeUInt);
			dict.Add(typeof(float), StackTypeDescription.Float32);
			dict.Add(typeof(double), StackTypeDescription.Float64);

			return dict;
		}


		/// <summary>
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		private StackTypeDescription GetStackTypeDescription(Type type)
		{
			Type elementtype;

			if (type.IsByRef || type.IsPointer)
			{
				elementtype = type.GetElementType();
			}
			else
				elementtype = type;


			StackTypeDescription std;
			if (elementtype.IsPrimitive)
			{
				std = s_metadataCilTypes[elementtype];
			}
			else
			{
				TypeDescription td = GetTypeDescription(elementtype);
				std = new StackTypeDescription(td);
			}


//			if (type.IsByRef || type.IsPointer)
//			{
//				td = GetTypeDescription(type.GetElementType());
//			}
//			else
//			{
//				td = GetTypeDescription(type);
//			}
//
//			StackTypeDescription std;
//			if (td.Type.IsPrimitive)
//			{
//				return s_metadataCilTypes[(uint) td.Type.MetadataToken];
//			}
//			else
//			{
//				std = new StackTypeDescription(td);
//			}

			if (type.IsByRef)
				std = std.GetManagedPointer();
			if (type.IsPointer)
				std = std.GetPointer();

			return std;
		}

		/// <summary>
		/// Computes the result type of binary numeric operations given the specified input types.
		/// Computation is done according to table 2 and table 7 in the CIL spec plus intuition.
		/// </summary>
		/// <returns></returns>
		private static StackTypeDescription GetNumericResultType(StackTypeDescription tleft, StackTypeDescription tright)
		{
			// We are relying on the fact that the enumeration values are sorted by size.
			if (tleft.CliBasicType == tright.CliBasicType)
			{
				if (!tleft.IsByRef)
					return new StackTypeDescription(tleft.CliBasicType,
					                                (CliNumericSize) Math.Max((int) tleft.NumericSize, (int) tright.NumericSize),
					                                tleft.IsSigned);
				else
					return StackTypeDescription.NativeInt;
			}

			if (tleft.IsByRef || tright.IsByRef)
			{
				if (tleft.IsByRef && tright.IsByRef)
					return StackTypeDescription.NativeInt;

				return tleft;
			}

			if (tleft.CliBasicType == CliBasicType.NativeInt || tright.CliBasicType == CliBasicType.NativeInt)
				return tleft;

			throw new ArgumentException(
				string.Format("Argument types are not valid cil binary numeric opcodes: Left: {0}; right: {1}.", tleft.CliType,
				              tright.CliType));
		}

		#endregion
	}
}