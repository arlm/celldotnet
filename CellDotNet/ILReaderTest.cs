using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using NUnit.Framework;

namespace CellDotNet
{
	/// <summary>
	/// TODO: Instruction pattern tests. Test offsets.
	/// </summary>
	[TestFixture]
	public class ILReaderTest : UnitTest
	{
		[Test, Explicit]
		public void GenerateReflectionCode()
		{
			StringBuilder enumcode = new StringBuilder();
			enumcode.AppendFormat(@"
	// This enum is generated by {0}. DO NOT EDIT.
	enum IRCode
	{{
", GetType().FullName);

			StringBuilder opcodewriter = new StringBuilder();
			opcodewriter.AppendFormat(@"
	// This class is generated by {0}. DO NOT EDIT.
	partial class IROpCodes
	{{
", GetType().FullName);

			FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);
			foreach (FieldInfo fi in fields)
			{
				OpCode oc = (OpCode)fi.GetValue(null);

				if (oc.OpCodeType == OpCodeType.Macro)
				{
//					continue;
					// We generally don't want macros, but these are okay...
					if (oc != OpCodes.Blt && oc != OpCodes.Ble &&
						oc != OpCodes.Blt_Un && oc != OpCodes.Ble_Un &&
						oc != OpCodes.Beq && oc != OpCodes.Bne_Un &&
						oc != OpCodes.Bge && oc != OpCodes.Bgt &&
						oc != OpCodes.Bge_Un && oc != OpCodes.Bgt_Un)
					{
						Console.WriteLine("Skipping opcode: {0}.", oc.Name);
						continue;
					}
				}

				enumcode.AppendFormat("		{0},\r\n", fi.Name);
				opcodewriter.AppendFormat(
					"		public static readonly IROpCode {0} = new IROpCode(\"{1}\", IRCode.{0}, FlowControl.{2}, OpCodeType.{3}, OperandType.{4}, StackBehaviour.{5}, StackBehaviour.{6}, OpCodes.{7});\r\n",
					fi.Name, oc.Name, oc.FlowControl, oc.OpCodeType, oc.OperandType, oc.StackBehaviourPush, oc.StackBehaviourPop, fi.Name);
			}

			enumcode.Append(@"
	}
");
			opcodewriter.Append(@"
	}
");

			Console.Write(enumcode.ToString());
			Console.Write(opcodewriter.ToString());
		}

		[Test]
		public void BasicParseTest()
		{
			int sharedvar = 100;
			Converter<int, int> del = delegate // (int obj)
								{
									int i = 500;
									i = i + sharedvar;
									List<int> list = new List<int>(234);
									list.Add(888);
									int j = Math.Max(3, 5);

									for (int n = 0; n < j; n++)
										list.Add(n);
									return i;
								};

			ILReader r = new ILReader(del.Method);
			int icount = 0;
			while (r.Read())
			{
				if (icount > 100)
					throw new Exception("Too many instructions.");

				icount++;
			}

			if (icount < 5)
				throw new Exception("too few instructions.");
		}

		private delegate void BasicTestDelegate();

		[Test]
		public void TestLoadInt32()
		{
			BasicTestDelegate del = delegate
			                        	{
			                        		int i = 0x0a0b0c0d;
			                        		Math.Abs(i);
			                        	};
			ILReader r = new ILReader(del.Method);

			bool found = false;
			while (r.Read())
			{
				if (r.OpCode == IROpCodes.Ldc_I4)
				{
					found = true;
					int val = (int) r.Operand;
					AreEqual(0x0a0b0c0d, val);
				}
			}

			IsTrue(found);
		}

		[Test]
		public void TestLoadInt64()
		{
			BasicTestDelegate del = delegate
										{
											long i = 0x0102030405060708L;
											Math.Abs(i);
										};
			ILReader r = new ILReader(del.Method);

			bool found = false;
			while (r.Read())
			{
				if (r.OpCode == IROpCodes.Ldc_I8)
				{
					found = true;
					long val = (long)r.Operand;
					AreEqual(0x0102030405060708L, val);
				}
			}

			IsTrue(found);
		}

		[Test]
		public void TestLoadString()
		{
			BasicTestDelegate del = delegate
										{
											string s = "hey";
											s.ToString();
										};
			ILReader r = new ILReader(del.Method);

			bool found = false;
			while (r.Read())
			{
				if (r.OpCode == IROpCodes.Ldstr)
				{
					found = true;
					string s = (string) r.Operand;
					AreEqual("hey", s);
				}
			}

			IsTrue(found);
		}

		[Test]
		public void TestLoadFloat()
		{
			BasicTestDelegate del = delegate
										{
											float s = 4.5f;
											s.ToString();
										};
			ILReader r = new ILReader(del.Method);

			bool found = false;
			while (r.Read())
			{
				if (r.OpCode == IROpCodes.Ldc_R4)
				{
					found = true;
					float f = (float) r.Operand;
					AreEqual(4.5f, f);
				}
			}

			IsTrue(found);
		}

		[Test]
		public void TestLoadDouble()
		{
			BasicTestDelegate del = delegate
										{
											double s = 4.5d;
											s.ToString();
										};
			ILReader r = new ILReader(del.Method);

			bool found = false;
			while (r.Read())
			{
				if (r.OpCode == IROpCodes.Ldc_R8)
				{
					found = true;
					double d = (double)r.Operand;
					AreEqual(4.5d, d);
				}
			}

			IsTrue(found);
		}

	}
}
