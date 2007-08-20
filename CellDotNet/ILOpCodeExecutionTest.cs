using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using NUnit.Framework;

namespace CellDotNet
{
	[TestFixture]
	public class ILOpCodeExecutionTest : UnitTest
	{
		[Test]
		public void Test_Ret()
		{
			ILWriter w = new ILWriter();

			w.WriteOpcode(OpCodes.Ldc_I4_7);
			w.WriteOpcode(OpCodes.Ret);

			Execution(w, 7);
		}

		[Test]
		public void Test_Add_I4()
		{
			ILWriter w = new ILWriter();

			w.WriteOpcode(OpCodes.Ldc_I4_7);
			w.WriteOpcode(OpCodes.Ldc_I4_3);
			w.WriteOpcode(OpCodes.Add);
			w.WriteOpcode(OpCodes.Ret);

			Execution(w, 10);
		}

		[Test]
		public void Test_Sub_I4()
		{
			ILWriter w = new ILWriter();

			w.WriteOpcode(OpCodes.Ldc_I4_7);
			w.WriteOpcode(OpCodes.Ldc_I4_3);
			w.WriteOpcode(OpCodes.Sub);
			w.WriteOpcode(OpCodes.Ret);

			Execution(w, 4);
		}

		[Test]
		public void Test_Mul_I4()
		{
			InstTest(OpCodes.Mul, 5, 3, 15);
		}

		[Test]
		public void Test_Ceq_I4()
		{
			InstTest(OpCodes.Ceq, 5, 3, 0);
			InstTest(OpCodes.Ceq, 5, 5, 1);
		}

		[Test]
		public void Test_Cgt_I4()
		{
			InstTest(OpCodes.Cgt, 5, 3, 1);
			InstTest(OpCodes.Cgt, 5, 5, 0);
			InstTest(OpCodes.Cgt, 5, 7, 0);
		}

		public void InstTest(OpCode opcode, int i1, int i2, int exp)
		{
			ILWriter w = new ILWriter();

			w.WriteOpcode(OpCodes.Ldc_I4);
			w.WriteInt32(i1);
			w.WriteOpcode(OpCodes.Ldc_I4);
			w.WriteInt32(i2);
			w.WriteOpcode(opcode);
			w.WriteOpcode(OpCodes.Ret);

			ILReader r = w.CreateReader();
			Console.WriteLine("Testing " + opcode.Name);
			while (r.Read())
				Console.WriteLine("{0} {1}", r.OpCode.Name, r.Operand);

			Execution(w, exp);
		}


		private static void Execution<T>(ILWriter ilcode, T expetedValue) where T : struct
		{
			RegisterSizedObject returnAddressObject = new RegisterSizedObject();
			returnAddressObject.Offset = Utilities.Align16(0x1000);

			IRTreeBuilder builder = new IRTreeBuilder();
			List<MethodVariable> vars = new List<MethodVariable>();
			List<IRBasicBlock> basicBlocks = builder.BuildBasicBlocks(ilcode.CreateReader(), vars);

			RecursiveInstructionSelector sel = new RecursiveInstructionSelector();

			ReadOnlyCollection<MethodParameter> par = new ReadOnlyCollection<MethodParameter>(new List<MethodParameter>());

			SpuManualRoutine spum = new SpuManualRoutine(false);

			spum.Writer.BeginNewBasicBlock();
			SpuAbiUtilities.WriteProlog(2, spum.Writer);

			sel.GenerateCode(basicBlocks, par, spum.Writer);

			// TODO Det h�ndteres muligvis ikke virtuelle moves i SimpleRegAlloc.
			new SimpleRegAlloc().alloc(spum.Writer.BasicBlocks);

			RegAllocGraphColloring.RemoveRedundantMoves(spum.Writer.BasicBlocks);

			spum.Offset = 1024;
			spum.PerformAddressPatching();

			SpuInitializer spuinit = new SpuInitializer(spum, returnAddressObject);
			spuinit.Offset = 0;
			spuinit.PerformAddressPatching();

			int[] initcode = spuinit.Emit();
			int[] methodcode = spum.Emit();

			Assert.Less(initcode.Length * 4, 1025, "SpuInitializer code is to large", null);

			int[] code = new int[1024/4 + methodcode.Length];

			Buffer.BlockCopy(initcode, 0, code, 0, initcode.Length*4);
			Buffer.BlockCopy(methodcode, 0, code, 1024, methodcode.Length*4);

			if (!SpeContext.HasSpeHardware)
				return;

			using (SpeContext ctx = new SpeContext())
			{
				ctx.LoadProgram(code);
				ctx.Run();

				T returnValue = ctx.DmaGetValue<T>((LocalStorageAddress) returnAddressObject.Offset);

				AreEqual(expetedValue, returnValue, "SPU delegate execution returned a wrong value.");
			}
		}
	}
}
