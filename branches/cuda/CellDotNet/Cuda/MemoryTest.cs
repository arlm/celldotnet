﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace CellDotNet.Cuda
{
	[TestFixture]
	public class MemoryTest : UnitTest
	{
		[Test]
		public void TestArrayStoreMemoryCopy()
		{
			Action<int, float, float[]> del = (index, value, arr) =>
			{
				arr[index] = value;
			};
			using (var kernel = CudaKernel.Create(del))
			{
				const int index = 5;
				const float value = 50f;

				var devmem = kernel.Context.AllocateLinear<float>(10);

				kernel.SetBlockSize(16, 16);
				kernel.SetGridSize(1, 1);
				kernel.ExecuteUntyped(index, value, devmem);

				var arr = new float[devmem.Length];
				kernel.Context.CopyDeviceToHost(devmem, 0, arr, 0, arr.Length);

				AreEqual(value, arr[index]);
			}
		}

		[Test]
		public void TestArrayStoreNonOverlapping()
		{
			Action<int[]> del = (arr) =>
			{
				arr[0] = int.MaxValue;
				arr[1] = -5;
			};
			using (var kernel = CudaKernel.Create(del))
			{
				var devmem = kernel.Context.AllocateLinear<int>(10);

				kernel.SetBlockSize(16, 16);
				kernel.SetGridSize(1, 1);
				kernel.ExecuteUntyped(devmem);

				var arr = new int[devmem.Length];
				kernel.Context.CopyDeviceToHost(devmem, 0, arr, 0, arr.Length);

				AreEqual(int.MaxValue, arr[0]);
				AreEqual(-5, arr[1]);
			}
		}

		[StaticArray(2)]
		private static Shared1D<int> sharedbufferI4;

		[Test]
		public void TestSharedMemory_I4()
		{
			// TODO: Should also test use of same buffer from different methods.
			// TODO: Should also test use of multiple buffers to check that they are being allocated with a correct combination of element size and count, so that two buffers don't overlap.
			Action<int[]> del = arr =>
			{
				sharedbufferI4[ThreadIndex.X] = ThreadIndex.X + 1;
				CudaRuntime.SyncThreads();
				arr[0] = sharedbufferI4[0] + sharedbufferI4[1];
			};
			using (var kernel = CudaKernel.Create(del))
			{
				kernel.PerformProcessing(CudaKernelCompileState.Complete);
				IsTrue(kernel.GetPtx().IndexOf("ld.shared") != -1);
				IsTrue(kernel.GetPtx().IndexOf("st.shared") != -1);

				var devmem = kernel.Context.AllocateLinear<int>(16);
				kernel.SetGridSize(1, 1);
				kernel.SetBlockSize(2, 1);
				kernel.ExecuteUntyped(devmem);
				var ret = new int[devmem.Length];
				kernel.Context.CopyDeviceToHost(devmem, 0, ret, 0, devmem.Length);

				Console.WriteLine(kernel.GetPtx());

				AreEqual(3, ret[0]);
			}
		}

		[StaticArray(2)]
		private static Shared1D<uint> sharedbufferU4;

		[Test]
		public void TestSharedMemory_U4()
		{
			Action<uint[]> del = arr =>
			{
				sharedbufferU4[ThreadIndex.X] = (uint) (ThreadIndex.X + 1);
				CudaRuntime.SyncThreads();
				arr[0] = sharedbufferU4[0] + sharedbufferU4[1];
			};
			using (var kernel = CudaKernel.Create(del))
			{
				kernel.PerformProcessing(CudaKernelCompileState.Complete);
				IsTrue(kernel.GetPtx().IndexOf("ld.shared") != -1);
				IsTrue(kernel.GetPtx().IndexOf("st.shared") != -1);

				var devmem = kernel.Context.AllocateLinear<uint>(16);
				kernel.SetGridSize(1, 1);
				kernel.SetBlockSize(2, 1);
				kernel.ExecuteUntyped(devmem);
				var ret = new uint[devmem.Length];
				kernel.Context.CopyDeviceToHost(devmem, 0, ret, 0, devmem.Length);

				Console.WriteLine(kernel.GetPtx());

				AreEqual(3u, ret[0]);
			}
		}

		[StaticArray(2)]
		private static Shared1D<float> sharedbufferR4;

		[Test]
		public void TestSharedMemory_R4()
		{
			Action<float[]> del = arr =>
			{
				sharedbufferR4[ThreadIndex.X] =  ThreadIndex.X + 1;
				CudaRuntime.SyncThreads();
				arr[0] = sharedbufferR4[0] + sharedbufferR4[1];
			};
			using (var kernel = CudaKernel.Create(del))
			{
				kernel.PerformProcessing(CudaKernelCompileState.Complete);
				IsTrue(kernel.GetPtx().IndexOf("ld.shared") != -1);
				IsTrue(kernel.GetPtx().IndexOf("st.shared") != -1);

				var devmem = kernel.Context.AllocateLinear<float>(16);
				kernel.SetGridSize(1, 1);
				kernel.SetBlockSize(2, 1);
				kernel.ExecuteUntyped(devmem);
				var ret = new float[devmem.Length];
				kernel.Context.CopyDeviceToHost(devmem, 0, ret, 0, devmem.Length);

				Console.WriteLine(kernel.GetPtx());

				AreEqual(3f, ret[0]);
			}
		}
	}
}
