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
		S7AddressPatchingDone,
		S8Complete
	}

	/// <summary>
	/// Data used during compilation of a method.
	/// </summary>
	internal class MethodCompiler : SpuRoutine
	{
		private MethodCompileState _state;

		public MethodCompileState State
		{
			get { return _state; }
			private set { _state = value; }
		}

		private List<IRBasicBlock> _blocks = new List<IRBasicBlock>();

		public List<SpuBasicBlock> SpuBasicBlocks
		{
			get
			{
				return GetBodyWriter().BasicBlocks;
			}
		}

		public List<IRBasicBlock> Blocks
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

		private List<MethodVariable> _variablesMutable;
		private ReadOnlyCollection<MethodVariable> _variables;
		/// <summary>
		/// Local variables.
		/// </summary>
		public ReadOnlyCollection<MethodVariable> Variables
		{
			get { return _variables; }
		}

		public override int Size
		{
			get { return GetSpuInstructionCount()*4; }
		}

		public MethodCompiler(MethodBase method)
		{
			_methodBase = method;
			State = MethodCompileState.S1Initial;

			PerformIRTreeConstruction();
//			new TypeDeriver().DeriveTypes(Blocks);
		}

		public void VisitTreeInstructions(Action<TreeInstruction> action)
		{
			IRBasicBlock.VisitTreeInstructions(Blocks, action);
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

			TypeDeriver typederiver = new TypeDeriver();


			// Build Parameters.
			List<MethodParameter> parlist = new List<MethodParameter>();
			int i = 0;
			foreach (ParameterInfo pi in _methodBase.GetParameters())
			{
				Utilities.Assert(pi.Position == i, "pi.Index == i");
				i++;
					
				parlist.Add(new MethodParameter(pi, typederiver.GetStackTypeDescription(pi.ParameterType)));
			}
			_parameters = new ReadOnlyCollection<MethodParameter>(parlist);


			// Build Variables.
			List<MethodVariable> varlist = new List<MethodVariable>();
			i = 0;
			foreach (LocalVariableInfo lv in _methodBase.GetMethodBody().LocalVariables)
			{
				Utilities.Assert(lv.LocalIndex == i, "lv.LocalIndex == i");
				i++;

				varlist.Add(new MethodVariable(lv, typederiver.GetStackTypeDescription(lv.LocalType)));
			}
			_variables = new ReadOnlyCollection<MethodVariable>(varlist);
			_variablesMutable = varlist;


			ILReader reader = new ILReader(_methodBase);
			_blocks = new IRTreeBuilder().BuildBasicBlocks(MethodBase, reader, _variablesMutable, _parameters);
			CheckTreeInstructionCountIsMinimum(reader.InstructionsRead);

			State = MethodCompileState.S2TreeConstructionDone;
		}

		/// <summary>
		/// Checks that the number of instructions in the constructed tree is equal to the number of IL instructions in the cecil model.
		/// </summary>
		/// <param name="minimumCount"></param>
		private void CheckTreeInstructionCountIsMinimum(int minimumCount)
		{
			int count = 0;
			VisitTreeInstructions(delegate
			{
				count += 1;
			});

			if (count < minimumCount)
			{
				TreeDrawer td= new TreeDrawer();
				td.DrawMethod(this);
				string msg = string.Format("Invalid tree instruction count of {0} for method {2}. Should have been {1}.", 
					count, minimumCount, MethodBase.Name);
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

			// Move calle-saves regs to virtual regs.
			_instructions.BeginNewBasicBlock();
			List<VirtualRegister> calleTemps = new List<VirtualRegister>(48);
			for (int regnum = 80; regnum <= 127; regnum++)
			{
				VirtualRegister temp = NextRegister();
				calleTemps.Add(temp);
				_instructions.WriteMove(HardwareRegister.GetHardwareRegister(regnum), temp);
			}

			// Generate the body.
			RecursiveInstructionSelector selector = new RecursiveInstructionSelector();
			selector.GenerateCode(this, _instructions);

			// Move callee saves temps back to physical regs.
			_instructions.BeginNewBasicBlock();
			for (int regnum = 80; regnum <= 127; regnum++)
			{
				_instructions.WriteMove(calleTemps[regnum - 80], HardwareRegister.GetHardwareRegister(regnum));
			}

			State = MethodCompileState.S4InstructionSelectionDone;
		}

		/// <summary>
		/// This is only for unit test
		/// </summary>
		/// <returns></returns>
		public SpuInstructionWriter GetBodyWriter()
		{
			if (State < MethodCompileState.S4InstructionSelectionDone)
				throw new InvalidOperationException("State < MethodCompileState.S4InstructionSelectionDone");

			return _instructions;
		}

		/// <summary>
		/// Return the number of instructions currently in the prolog, body and epilog.
		/// </summary>
		/// <returns></returns>
		public int GetSpuInstructionCount()
		{
			if (State < MethodCompileState.S4InstructionSelectionDone)
				throw new InvalidOperationException("Too early. State: " + State);

			int count = 
				_prolog.GetInstructionCount() + 
				_instructions.GetInstructionCount() + 
				_epilog.GetInstructionCount();

			return count;
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

			if (State < MethodCompileState.S7AddressPatchingDone && targetState >= MethodCompileState.S7AddressPatchingDone)
				PerformAddressPatching();

			if (targetState >= MethodCompileState.S8Complete)
			{
				if (targetState <= MethodCompileState.S8Complete) 
					throw new NotImplementedException("Target state: " + targetState);
				else 
					throw new ArgumentException("Invalid state: " + targetState, "targetState");
			}
		}

		private void PerformRegisterAllocation()
		{
			AssertState(MethodCompileState.S4InstructionSelectionDone);

			RegAllocGraphColloring regalloc = new RegAllocGraphColloring();
			regalloc.Alloc(SpuBasicBlocks, GetNewSpillOffset);

//			SimpleRegAlloc regalloc = new SimpleRegAlloc();
//			List<SpuInstruction> asm = _instructions.GetAsList();
//			regalloc.alloc(asm, 16);

			State = MethodCompileState.S5RegisterAllocationDone;
		}

		private int _nextSpillOffset = 3; // Start by pointing to start of Local Variable Space.
		/// <summary>
		/// For the register allocator to use to get SP offsets for spilling.
		/// </summary>
		/// <returns></returns>
		public int GetNewSpillOffset()
		{
			return _nextSpillOffset++;
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

		/// <summary>
		/// Writes outer prolog.
		/// </summary>
		/// <param name="prolog"></param>
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

			// Save LR in caller's frame.
			prolog.WriteStqd(HardwareRegister.LR, HardwareRegister.SP, 1);

			// Establish new SP.
			prolog.WriteSfi(HardwareRegister.SP, HardwareRegister.SP, -frameSlots*16);

			// Store SP at new frame's Back Chain.
			prolog.WriteStqd(HardwareRegister.SP, HardwareRegister.SP, 0);
		}

		/// <summary>
		/// Writes inner epilog.
		/// </summary>
		/// <param name="epilog"></param>
		private void WriteEpilog(SpuInstructionWriter epilog)
		{
			// Assume that the code that wants to return has placed the return value in the correct
			// registers (R3+).

			// Restore old SP.
			epilog.WriteLqd(HardwareRegister.SP, HardwareRegister.SP, 0);

			// TODO: Restore caller-saves.

			// Restore old LR from callers frame.
			epilog.WriteLqd(HardwareRegister.LR, HardwareRegister.SP, 1);

			// Return.
			epilog.WriteBi(HardwareRegister.LR);
		}

		/// <summary>
		/// For unit testing.
		/// </summary>
		/// <returns></returns>
		public SpuInstructionWriter GetPrologWriter()
		{
			return _prolog;
		}

		/// <summary>
		/// For unit testing.
		/// </summary>
		/// <returns></returns>
		public SpuInstructionWriter GetEpilogWriter()
		{
			return _epilog;
		}


		#endregion

		/// <summary>
		/// Inserts offsets for local branches and call. That is, for instrukctions containing 
		/// <see cref="SpuBasicBlock"/> and <see cref="ObjectWithAddress"/> objects.
		/// </summary>
		public override void PerformAddressPatching()
		{
			AssertState(MethodCompileState.S6PrologAndEpilogDone);

			// Iterate bbs, instructions to determine bb offsets and collect branch instructions,
			// so that the branch instructions afterwards can be patched with the bb addresses.

			Utilities.Assert(_prolog.BasicBlocks.Count > 0, "_prolog.BasicBlocks.Count == 0");
			Utilities.Assert(_epilog.BasicBlocks.Count > 0, "_epilog.BasicBlocks.Count == 0");

			List<SpuBasicBlock> bblist = new List<SpuBasicBlock>();
			bblist.Add(_prolog.BasicBlocks[0]);
			bblist.AddRange(_instructions.BasicBlocks);
			bblist.Add(_epilog.BasicBlocks[0]);


			PerformAddressPatching(bblist, _epilog.BasicBlocks[0]);


			State = MethodCompileState.S7AddressPatchingDone;
		}

		private SpuInstructionWriter _instructions;

		public override int[] Emit()
		{
			int[] prologbin = SpuInstruction.emit(GetPrologWriter().GetAsList());
			int[] bodybin = SpuInstruction.emit(GetBodyWriter().GetAsList());
			int[] epilogbin = SpuInstruction.emit(GetEpilogWriter().GetAsList());

			int[] combined = new int[prologbin.Length + bodybin.Length + epilogbin.Length];
			Buffer.BlockCopy(prologbin, 0, combined, 0, prologbin.Length);
			Buffer.BlockCopy(bodybin, 0, combined, prologbin.Length, bodybin.Length);
			Buffer.BlockCopy(epilogbin, 0, combined, prologbin.Length + bodybin.Length, epilogbin.Length);

			return combined;
		}
	}
}