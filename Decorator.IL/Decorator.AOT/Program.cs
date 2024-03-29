﻿using Mono.Cecil;

using System;

namespace Decorator.AOT
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Console.WriteLine($"Modifying ({args.Length}) assemblies...");

			foreach (var assemblyPath in args)
			{
				var module = ModuleDefinition.ReadModule(assemblyPath);

				// TODO: modify and make an AOT assembly

				module.Write(assemblyPath);
			}

			Console.WriteLine($"Done!");
		}
	}
}