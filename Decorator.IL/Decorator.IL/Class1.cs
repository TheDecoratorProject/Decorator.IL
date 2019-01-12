using StrictEmit;

using SwissILKnife;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Decorator.IL
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class AttatchAttribute : Attribute
	{
		public AttatchAttribute(Type attatchTo)
		{
		}

		public Type AttatchedOn { get; }
	}

	public interface IAttatchment
	{
		void EmitDeserialize(ILGenerator il);

		void EmitSerialize(ILGenerator il);

		void EmitEstimateSize(ILGenerator il);
	}

	[Attatch(typeof(RequiredAttribute.RequiredDecoration<>))]
	public class RequiredAttatchment<T> : IAttatchment
	{
		public void EmitDeserialize(ILGenerator il)
		{
			var local = il.DeclareLocal(typeof(object));
			var isntIt = il.DefineLabel();

			// object objVal = array[i];
			ilMethods.LoadCurrentObject();
			il.EmitSetLocalVariable(local);

			// if (!(objVal is T))
			il.EmitLoadLocalVariable(local);
			il.EmitIsInstance<T>();
			il.EmitShortBranchTrue(isntIt);

			// if it's not a value type, we can check if it's null (since reference types can be null)
			if (!_valueType)
			{
				// || objVal == null))
				il.EmitLoadLocalVariable(local);
				il.EmitShortBranchFalse(isntIt);
			}

			// return false;
			il.EmitConstantInt(0);
			il.EmitReturn();

			il.MarkLabel(isntIt);

			// i++;
			ilMethods.AddToIndex(() => il.EmitConstantInt(1));

			// result.Property = (T)objVal;
			ilMethods.SetMemberValue(() =>
			{
				il.EmitLoadLocalVariable(local);
			});
		}

		public void EmitEstimateSize(ILGenerator ilGen)
		{
		}

		public void EmitSerialize(ILGenerator ilGen)
		{
		}
	}

	// TODO: make these
	/*
	[Attatch(typeof(OptionalAttribute.OptionalDecoration<>))]
	public class OptionalAttatchment<T> : IAttatchment
	{
	}

	[Attatch(typeof(Ignored))]
	public class IgnoredAttatchment<T> : IAttatchment
	{
	}
	*/

	public static class AttatchmentCache
	{
		// TODO: limit assembly search
		// <TheTypeSpecifiedInTheAttatchmentAttribute, TheTypeTheAttributeWasAppliedOn>
		public static Dictionary<Type, Type> AttatchmentToDecoration = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly => assembly.GetTypes())
				.Where(type => type.GetCustomAttribute<AttatchAttribute>() != null)
				.ToDictionary
				(
					type => type.GetCustomAttribute<AttatchAttribute>().AttatchedOn
				);
	}

	public class ILCompiler<T> : ICompiler<T>
	{
		private readonly ICompiler<T> _compiler;
		private readonly Dictionary<Type, Type> _attatchmentToDecoration;

		public ILCompiler(ICompiler<T> compiler)
		{
			_compiler = compiler;

			_attatchmentToDecoration = AttatchmentCache.AttatchmentToDecoration;
		}

		public IDecoration[] Compile(IDiscovery<T> discovery, IDecorationFactoryBuilder builder)
		{
			var decorations = _compiler.Compile(discovery, builder);

			foreach (var decoration in decorations)
			{
				var decorationType = decoration.GetType();
				var genArgs = decorationType.GenericTypeArguments;
				var typeWithoutGen = decorationType.GetGenericTypeDefinition();

				// TODO: exception if not exist
				var attatchmentType = _attatchmentToDecoration[typeWithoutGen];
				attatchmentType.MakeGenericType(genArgs);
				var attatchment = (IAttatchment)Activator.CreateInstance(attatchmentType);

				// TODO: something
			}

			return decorations;
		}

		public class Block
		{
			[Position(0), Required] public int X { get; set; }
			// etc.

			private object[] _data;

			[Array, Position(1)]
			public object[] Data
			{
				get => _data;
				set
				{
					_data = value;

					int i = 1;

					// todo: bounds checking
					switch (_data[0])
					{
						case "a":
							if (DDecorator<AParams>.TryDeserialize(Data, ref i, out var result))
							{
								BlockParams = result;
							}
							break;

						default: break;
					}
				}
			}

			public BlockParams BlockParams { get; set; }
		}

		public class BlockParams
		{
			// idk, base class
		}

		public class AParams : BlockParams
		{
			[Required, Position(0)] public int MusicId { get; set; }
		}

		public class ILDecoration : IDecoration
		{
			public bool Deserialize(ref object[] array, object instance, ref int index) => throw new NotImplementedException();

			public void Serialize(ref object[] array, object instance, ref int index) => throw new NotImplementedException();

			public void EstimateSize(object instance, ref int size) => throw new NotImplementedException();
		}

		public delegate bool Deserialize(ref object[] array, object instance, ref int index);

		public delegate void Serialize(ref object[] array, object instance, ref int index);

		public delegate void EstimateSize(object instance, ref int index);

		// will be overriden by AOT in the future
		public interface IMethodGenerator
		{
			ILGenerator ILDeserialize { get; }
			ILGenerator ILSerialize { get; }
			ILGenerator ILEstimateSize { get; }

			(Deserialize deserialize, Serialize serialize, EstimateSize estimateSize) Create();
		}

		public class MethodGenerator : IMethodGenerator
		{
			private DynamicMethod _deserialize;
			private DynamicMethod _serialize;
			private DynamicMethod _estimateSize;

			public MethodGenerator()
			{
				_deserialize = new DynamicMethod(string.Empty, typeof(bool), new[] { typeof(object[]).MakeByRefType(), typeof(object), typeof(int).MakeByRefType() });
				_serialize = new DynamicMethod(string.Empty, typeof(void), new[] { typeof(object[]).MakeByRefType(), typeof(object), typeof(int).MakeByRefType() });
				_estimateSize = new DynamicMethod(string.Empty, typeof(void), new[] { typeof(object), typeof(int).MakeByRefType() });

				ILDeserialize = _deserialize.GetILGenerator();
				ILSerialize = _serialize.GetILGenerator();
				ILEstimateSize = _estimateSize.GetILGenerator();
			}

			public ILGenerator ILDeserialize { get; }
			public ILGenerator ILSerialize { get; }
			public ILGenerator ILEstimateSize { get; }

			public (Deserialize deserialize, Serialize serialize, EstimateSize estimateSize) Create()
				=>
				(
					_deserialize.CreateDelegate<Deserialize>(),
					_serialize.CreateDelegate<Serialize>(),
					_estimateSize.CreateDelegate<EstimateSize>()
				);
		}
	}
}