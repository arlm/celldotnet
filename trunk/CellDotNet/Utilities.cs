using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace CellDotNet
{
	static class Utilities
	{
		#region Asserts.

		static public void AssertArgumentNotNull(object arg, string paramName)
		{
			if (arg == null)
				throw new ArgumentNullException(paramName);
		}

		static public string GetUnitTestName()
		{
			StackTrace st = new StackTrace(1);
			StackFrame[] frames = st.GetFrames();

			foreach (StackFrame f in frames)
			{
				MethodBase m = f.GetMethod();
				object[] list = m.GetCustomAttributes(typeof (TestAttribute), false);
				if (list.Length != 0)
				{
					return m.Name;
				}
			}
			return "";
		}

		static public void AssertArgument(bool condition, string message)
		{
			if (!condition)
				throw new ArgumentException(message);
		}

		static public void AssertArgumentRange(bool rangeCondition, string paramName, object actualValue, string message)
		{
			if (!rangeCondition)
				throw new ArgumentOutOfRangeException(paramName, actualValue, message);
		}

		static public void AssertArgumentRange(bool rangeCondition, string paramName, object actualValue)
		{
			if (!rangeCondition)
				throw new ArgumentOutOfRangeException(paramName, actualValue, "The value is out of range.");
		}

		static public void AssertNotNull(object arg, string expressionOrMessage)
		{
			if (arg == null)
				throw new Exception("An expression is null: " + expressionOrMessage);
		}

		static public void AssertNull(object arg, string expressionOrMessage)
		{
			if (arg != null)
				throw new Exception("An expression is not null: " + expressionOrMessage);
		}

		static public void Assert(bool condition, string message)
		{
			if (!condition)
				throw new Exception(message); // Should we use another exception?
		}

		#endregion

		static public bool TryGetFirst<T>(IEnumerable<T> enumerable, out T first)
		{
			first = default(T);

			using (IEnumerator<T> e = enumerable.GetEnumerator())
			{
				if (!e.MoveNext())
					return false;

				first = e.Current;
				return true;
			}
		}

		public static T GetFirst<T>(IEnumerable<T> set)
		{
			T first;

			if (!TryGetFirst(set, out first))
				throw new ArgumentException("Empty set.");

			return first;
		}

		/// <summary>
		/// You can use this one to make resharper think that a variable is used so that it won't
		/// show a warning. Can be handy when the variable isn't used for anything but debugging, 
		/// or when resharpers value analysis is making a mistake.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="var"></param>
		[Conditional("XXXY")]
		public static void PretendVariableIsUsed<T>(T var)
		{
			
		}

		public static IntPtr Align16(IntPtr value)
		{
			return (IntPtr) (((long)value + 15) & ~0xf);
		}

		public static int Align16(int value)
		{
			return (value + 15) & ~0xf;
		}

		public static bool IsQuadwordAligned(LocalStorageAddress lsa)
		{
			return IsQuadwordAligned(lsa.Value);
		}

		public static bool IsQuadwordAligned(int lsa)
		{
			return (lsa % 16) == 0;
		}

		public static bool IsQuadwordAligned(IntPtr ea)
		{
			return ((long)ea % 16) == 0;
		}

		public static bool IsQuadwordMultiplum(int bytecount)
		{
			return (bytecount % 16) == 0;
		}

		static public void CopyCode(int[] src, int srcOffset, int[] dest, int destOffset, int count)
		{
			Buffer.BlockCopy(src, srcOffset*4, dest, destOffset*4, count*4);
		}

		static public void DumpMemory(SpeContext context, LocalStorageAddress lsa, int bytecount, TextWriter writer)
		{
			AssertArgument(IsQuadwordAligned(lsa), "IsQuadwordAligned(lsa): " + lsa.Value.ToString("x6"));

			int[] mem = context.GetCopyOffLocalStorage();
			if (lsa.Value + bytecount > mem.Length * 4)
				throw new ArgumentException("Memory out of range.");
			DumpMemory(mem, lsa.Value / 4, lsa, bytecount, writer);
		}

		static public void DumpMemory(int[] memDump, int arrayOffset, LocalStorageAddress arrayOffsetAddress, int bytecount, TextWriter writer)
		{
			int bytesPerLine = 16;
			for (int i = 0; i < Math.Min(memDump.Length, bytecount / 4); i++)
			{
				int address = arrayOffsetAddress.Value + i*4;
				if (address % bytesPerLine == 0)
				{
					if (i > 0)
						writer.WriteLine();

					writer.Write("{0:x6}: ", address);
				}
				else if (address % 8 == 0)
					writer.Write(" ");
				else if (address % 4 == 0)
					writer.Write(" ");

				uint val = (uint)memDump[i];
				writer.Write(" {0:x2} {1:x2} {2:x2} {3:x2}", val >> 0x18, (val >> 0x10) & 0xff, (val >> 8) & 0xff, val & 0xff);
			}
			writer.WriteLine();
		}


		/// <summary>
		/// Writes the code to the file with instructions on how to compile it.
		/// The executable should be ready for debugging with spu-gdb.
		/// </summary>
		/// <param name="cc"></param>
		/// <param name="filename"></param>
		public static void WriteAssemblyToFile(CompileContext cc, string filename)
		{
			int[] code = cc.GetEmittedCode();

			using (StreamWriter writer = new StreamWriter(filename))
			{
				// Write the bytes and define main (which really is the initializer).
				writer.Write(@"
# Compile this file with the following command:
# spu-as {0} -o {1}.o && spu-gcc {1}.o -o {1}

  .section "".text""
  .globl main
  .type main, @function
main:
", Path.GetFileName(filename), Path.GetFileNameWithoutExtension(filename));

				int offset = 0;
				while (offset < code.Length)
				{
					const int wordPerLine = 2;
					writer.Write("  .byte ");
					for (int i = 0; i < wordPerLine && offset + i < code.Length; i++)
					{
						if (i > 0)
							writer.Write(", ");
						int inst = code[offset + i];

						writer.Write("0x" + ((byte)(inst >> 24)).ToString("x2"));
						writer.Write(", 0x" + ((byte)(inst >> 16)).ToString("x2"));
						writer.Write(", 0x" + ((byte)(inst >> 8)).ToString("x2"));
						writer.Write(", 0x" + ((byte)(inst >> 0)).ToString("x2"));
					}
					writer.WriteLine();
					offset += wordPerLine;
				}
				
				writer.WriteLine();
				writer.WriteLine();

				// Write the symbols.
				foreach (ObjectWithAddress obj in cc.GetAllObjectsForDisassembly())
				{
					// TODO: Encode object name.
					if (obj.Offset == 0)
						continue;
					string encodedname = obj.Name;
					// Anonymous methods contains '<' and '>' in their name.

					encodedname = encodedname.Replace("<", "").Replace('>', '$');

					writer.Write(@"
  .globl {0}
  .type {0}, @object
  .set {0}, main + {1}
", encodedname, obj.Offset);
				}
			}
		}
	}
}
