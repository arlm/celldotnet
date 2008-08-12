using System;
using System.Collections.Generic;
using System.Reflection;

namespace CellDotNet.Cuda
{
	public class CudaKernel<T> where T : class
	{
		private readonly T _kernelWrapperDelegate;
		private List<CudaMethod> _methods;
		private MethodInfo _kernelMethod;

		public CudaKernel(T kerneldelegate)
		{
			if (!(kerneldelegate is Delegate))
				throw new ArgumentException("Type argument must be a delegate type.");

			// TODO Generate LCG delegate wrapper.
			this._kernelWrapperDelegate = kerneldelegate;

			_kernelMethod = (kerneldelegate as Delegate).Method;
			Utilities.AssertArgument(_kernelMethod.IsStatic, "Kernel method must be static.");
			_methods = PerformIRConstruction(_kernelMethod);
		}

		static private List<CudaMethod> PerformIRConstruction(MethodInfo kernelMethod)
		{
			var methodmap = new Dictionary<MethodBase, CudaMethod>();
			var methodWorkList = new Stack<MethodBase>();
			var instructionsNeedingPatching = new List<ListInstruction>();

			// Construct CudaMethods by traversing the call graph.
			methodWorkList.Push(kernelMethod);
			while (methodWorkList.Count != 0)
			{
				MethodBase methodBase = methodWorkList.Pop();
				var cm = new CudaMethod(methodBase);
				cm.PerformProcessing(CudaMethod.CompileState.ListContructionDone);
				methodmap.Add(methodBase, cm);

				foreach (BasicBlock block in cm.Blocks)
				{
					foreach (ListInstruction inst in block.Instructions)
					{
						if (!(inst.Operand is MethodBase)) 
							continue;

						CudaMethod calledmethod;
						if (methodmap.TryGetValue((MethodBase) inst.Operand, out calledmethod))
						{
							// The encountered MethodBase has been encountered before.
							inst.Operand = calledmethod;
						}
						else
						{
							// The encountered MethodBase has not been encountered before, so it's pushed onto 
							// a work list, and make a note that the current instruction needs to be patched later.
							methodWorkList.Push((MethodBase)inst.Operand);
							instructionsNeedingPatching.Add(inst);
						}
					}
				}
			}

			foreach (ListInstruction inst in instructionsNeedingPatching)
			{
				inst.Operand = methodmap[(MethodBase) inst.Operand];
			}

			return new List<CudaMethod>(methodmap.Values);
		}

		public T Start
		{
			get { return _kernelWrapperDelegate; }
		}

		internal ICollection<CudaMethod> Methods
		{
			get { return _methods; }
		}

		public void StartTmp(object[] args)
		{
//			 BuildIRTree(_kernelMethod);
			CudaMethod cm = new CudaMethod(_kernelMethod);

			/// 1: Compile to PTX
			/// 2: Compile to cubin
			/// 3: Load cubin
			/// 4: Set up arguments/data.
			/// 5: Start kernel.
		}

		public void SetBlockSize(int x, int y)
		{
			throw new NotImplementedException();
		}

		public void SetGridSize(int x, int y)
		{
			throw new NotImplementedException();
		}
	}
}