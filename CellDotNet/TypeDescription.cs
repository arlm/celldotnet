using System;
using System.Collections.Generic;
using System.Text;

namespace CellDotNet
{
	/// <summary>
	/// This is our own representation of a type. It wraps a <see c="System.Type" /> and also
	/// contains runtime information about the type, such as field offsets and methods.
	/// 
	/// <para>
	/// Every TypeDescription instance is a complete type; that is, it does not represent a generic
	/// type without type parameters or an array without element type.
	/// </para>
	/// 
	/// TODO: Figure out what to do about generics.
	/// </summary>
	class TypeDescription
	{
		private Type _type;
		/// <summary>
		/// Contains the <see cref="System.Type"/> representation of the type.
		/// </summary>
		public Type Type
		{
			get { return _type; }
		}

		public TypeDescription(Type type)
		{
			if (type == null)
				throw new ArgumentNullException();

			if (type.IsByRef || type.IsPointer)
				throw new ArgumentException("Argument is & or *.");
			if (type.IsArray)
				throw new ArgumentException("Argument is array.");
			if (type.IsGenericTypeDefinition)
				throw new ArgumentException("Argument is a generic type.");

			_type = type;

			Initialize();
		}

		public TypeDescription(GenericType genericType, params StackTypeDescription[] genericParameters)
		{
			if (genericType == null)
				throw new ArgumentNullException();
			if (genericParameters == null || genericParameters.Length == 0)
				throw new ArgumentException("genericParameters");

//			// Create the type to verify that the combination is okay.
//			_type = genericType.Type.MakeGenericType(
//				Array.ConvertAll<StackTypeDescription, Type>(genericParameters, delegate(TypeDescription desc) { return desc.Type; }));

			_genericType = genericType;
			_genericParameters = genericParameters;

			Initialize();
		}

		private void Initialize()
		{
		}

		private GenericType _genericType;
		public GenericType GenericType
		{
			get { return _genericType; }
		}

		private StackTypeDescription[] _genericParameters;
		public IList<StackTypeDescription> GenericParameters
		{
			get { return _genericParameters; }
		}
	}

	/// <summary>
	/// A generic type without type parameters.
	/// </summary>
	class GenericType
	{
		private Type _type;
		/// <summary>
		/// The generic type without parameters.
		/// </summary>
		public Type Type
		{
			get { return _type; }
		}

		public GenericType(Type type)
		{
			if (type == null)
				throw new ArgumentNullException("type");
			if (!type.IsGenericTypeDefinition)
				throw new ArgumentException("type argument is not generic.");

			_type = type;
		}
	}
}
