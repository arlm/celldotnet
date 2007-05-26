using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace CellDotNet
{
	[TestFixture, Ignore("Only for generating code.")]
	public class CodeGenUtils
	{
		/// <summary>
		/// Returns all IL opcodes.
		/// </summary>
		/// <returns></returns>
		private List<OpCode> GetILOpcodes()
		{
			List<OpCode> list=  new List<OpCode>();
			FieldInfo[] fields = typeof(OpCodes).GetFields();

			foreach (FieldInfo fi in fields)
			{
				if (fi.FieldType != typeof(OpCode))
					continue;

				OpCode oc = (OpCode)fi.GetValue(null);
				list.Add(oc);
			}

			return list;
		}

		[Test]
		public void GenerateILFlowNextOpCodeSwitchCases()
		{
			TextWriter sw = Console.Out;

			foreach (OpCode oc in GetILOpcodes())
			{
				if (oc.FlowControl != FlowControl.Next)
					continue;

				if (oc.OpCodeType == OpCodeType.Macro)
					continue;

				sw.Write("\t\t\tcase Code.{0}: // {1}\r\n", oc.Code, oc.Name);
			}
		}

		/// <summary>
		/// Generates enumeration values 
		/// </summary>
		[Test]
		public void GenerateILEnumValues()
		{
			StringWriter sw = new StringWriter();

			foreach (OpCode oc in GetILOpcodes())
			{
				if (oc.OpCodeType == OpCodeType.Macro)
					sw.Write("\t\t// {0} = {1}, // {2} \r\n", oc.Code, (int) oc.Code, oc.OpCodeType);
				else
					sw.Write("\t\t{0} = {1}, // {2} \r\n", oc.Code, (int) oc.Code, oc.OpCodeType);
			}

			Console.Write(sw.GetStringBuilder().ToString());
		}


		/// <summary>
		/// Returns the SPU opcodes that are defined and checks that their field names are the same as the name that is given to the constructor.
		/// </summary>
		/// <returns></returns>
		private static List<SpuOpCode> GetSpuOpCodes()
		{
			FieldInfo[] fields = typeof (SpuOpCode).GetFields(BindingFlags.Static | BindingFlags.Public);

			List<SpuOpCode> opcodes = new List<SpuOpCode>();

			foreach (FieldInfo field in fields)
			{
				if (field.FieldType != typeof(SpuOpCode))
					continue;

				SpuOpCode oc = (SpuOpCode) field.GetValue(null);

				if (oc.Name != field.Name)
					throw new Exception(string.Format("Name of opcode field {0} is not the same as the opcode name ({1}).", field.Name, oc.Name));

				opcodes.Add(oc);
			}

			return opcodes;
		}

		/// <summary>
		/// Returns the qualified name of the static field that contains the field. 
		/// Used for generating the instruction writer methods.
		/// </summary>
		static string GetQualifiedOpcodeFieldName(SpuOpCode opcode)
		{
			return typeof (SpuOpCode).Name + "." + opcode.Name;
		}

		[Test]
		public void GenerateSpuInstructionWriterMethods()
		{
			StringWriter tw = new StringWriter();

			tw.Write(@"
	// THIS CLASS IS GENERATED BY {0}.{1} - DO NO EDIT. 
	partial class {2}
	{{
", GetType().FullName, "GenerateSpuInstructionWriterMethods()", typeof(SpuInstructionWriter).Name);

			List<SpuOpCode> list = GetSpuOpCodes();
			foreach (SpuOpCode opcode in list)
			{
				// capitalized name.
				string ocname = opcode.Name[0].ToString().ToUpper() + opcode.Name.Substring(1);

				List<string> regnames = new List<string>();
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Rt) != SpuOpCodeRegisterUsage.None)
					regnames.Add("rt");
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Ra) != SpuOpCodeRegisterUsage.None)
					regnames.Add("ra");
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Rb) != SpuOpCodeRegisterUsage.None)
					regnames.Add("rb");
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Rc) != SpuOpCodeRegisterUsage.None)
					regnames.Add("rc");


				StringBuilder declnewdest = new StringBuilder();
				StringBuilder declolddest = new StringBuilder();
				StringBuilder bodynewdest = new StringBuilder();
				StringBuilder bodyolddest = new StringBuilder();


				// Declaration.
				foreach (string name in regnames)
				{
					declolddest.Append((declolddest.Length != 0 ? ", " : "") + "VirtualRegister " + name);
					if (name != "rt" || opcode.NoRegisterWrite)
						declnewdest.Append((declnewdest.Length != 0 ? ", " : "") + "VirtualRegister " + name);
				}

				if (opcode.HasImmediate)
				{
					declnewdest.Append((declnewdest.Length != 0 ? ", " : "") + "int immediate");
					declolddest.Append((declolddest.Length != 0 ? ", " : "") + "int immediate");
				}

				// Body.
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Rt) != SpuOpCodeRegisterUsage.None)
				{
					if (opcode.NoRegisterWrite)
					{
						bodynewdest.Append("inst.Rt = rt;\r\n");
						bodyolddest.Append("inst.Rt = rt;\r\n");
					}
					else
					{
						bodynewdest.Append("inst.Rt = NextRegister();\r\n");
						bodyolddest.Append("inst.Rt = rt;\r\n");
					}
				}
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Ra) != SpuOpCodeRegisterUsage.None)
				{
					bodynewdest.Append("inst.Ra = ra;\r\n");
					bodyolddest.Append("inst.Ra = ra;\r\n");
				}
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Rb) != SpuOpCodeRegisterUsage.None)
				{
					bodynewdest.Append("inst.Rb = rb;\r\n");
					bodyolddest.Append("inst.Rb = rb;\r\n");
				}
				if ((opcode.RegisterUsage & SpuOpCodeRegisterUsage.Rc) != SpuOpCodeRegisterUsage.None)
				{
					bodynewdest.Append("inst.Rc = rc;\r\n");
					bodyolddest.Append("inst.Rc = rc;\r\n");
				}
				if (opcode.HasImmediate)
				{
					bodynewdest.Append("inst.Constant = immediate;\r\n");
					bodyolddest.Append("inst.Constant = immediate;\r\n");
				}
				bodynewdest.Append("AddInstruction(inst);");
				bodyolddest.Append("AddInstruction(inst);");
				bodynewdest.Append("return inst.Rt;");

				// Put it together.

				string methodformat = @"
		/// <summary>
		/// {0}
		/// </summary>
		public {3} Write{1}({2})
		{{
			SpuInstruction inst = new SpuInstruction({4});
			{5}
		}}
";
				// GetQualifiedOpcodeFieldName(opcode)
				tw.Write(methodformat, opcode.Title, ocname, declnewdest, "VirtualRegister", GetQualifiedOpcodeFieldName(opcode), bodynewdest);
				if (declolddest.Length != declnewdest.Length)
					tw.Write(methodformat, opcode.Title, ocname, declolddest, "void", GetQualifiedOpcodeFieldName(opcode), bodyolddest);
			}

			tw.Write(@"
	}
");

			Console.Write(tw.GetStringBuilder().ToString());
		}

	}

}
