//
// Gendarme.Rules.Correctness.MethodCanBeMadeStaticRule
//
// Authors:
//	Jb Evain <jbevain@gmail.com>
//	Sebastien Pouliot <sebastien@ximian.com>
//
// Copyright (C) 2007 Jb Evain
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using Mono.Cecil;
using Mono.Cecil.Cil;

using Gendarme.Framework;
using Gendarme.Framework.Engines;
using Gendarme.Framework.Rocks;

namespace Gendarme.Rules.Correctness {

	/// <summary>
	/// This rule checks for methods that do not require anything from the current
	/// instance. Those methods can be converted into static methods, which helps
	/// a bit the performance (the hidden <c>this</c> parameter can be omitted),
	/// and clarify the API.
	/// </summary>
	/// <example>
	/// Bad example:
	/// <code>
	/// public class Bad {
	///	private int x, y, z;
	///	
	///	bool Valid (int value)
	///	{
	///		// the validation has no need to know the instance values
	///		return (value > 0);
	///	}
	///	
	///	public int X {
	///		get {
	///			return x;
	///		}
	///		set {
	///			if (!Valid (value)) {
	///				throw ArgumentException ("X");
	///			}
	///			x = value;
	///		}
	///	}
	///	
	///	// same for Y and Z
	///}
	/// </code>
	/// </example>
	/// <example>
	/// Good example:
	/// <code>
	/// public class Good {
	///	private int x, y, z;
	///	
	///	static bool Valid (int value)
	///	{
	///		return (value > 0);
	///	}
	///	
	///	// same X (and Y and Z) as before
	///}
	/// </code>
	/// </example>

	[Problem ("This method does not use any instance fields, properties or methods and can be made static.")]
	[Solution ("Make this method static.")]
	[EngineDependency (typeof (OpCodeEngine))]
	[FxCopCompatibility ("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
	public class MethodCanBeMadeStaticRule : Rule, IMethodRule {

		public RuleResult CheckMethod (MethodDefinition method)
		{
			// we only check non static, non virtual methods and not constructors
			// we also don't check event callbacks, as they usually bring a lot of false positive,
			// and that developers are tied to their signature
			if (method.IsStatic || method.IsVirtual || method.IsConstructor || method.IsEventCallback ())
				return RuleResult.DoesNotApply;

			// we only check methods with a body
			if (!method.HasBody)
				return RuleResult.DoesNotApply;

			// that aren't compiler generated (e.g. anonymous methods) or generated by a tool (e.g. web services)
			if (method.IsGeneratedCode ())
				return RuleResult.DoesNotApply;

			// rule applies

			// if we find a use of the "this" reference, it's ok

			// in most (all?) case "this" is used with the ldarg_0 instruction
			if (OpCodeEngine.GetBitmask (method).Get (Code.Ldarg_0))
				return RuleResult.Success;

			// but it's also valid to have an ldarg with a 0 value operand
			foreach (Instruction instr in method.Body.Instructions) {
				if ((instr.OpCode.Code == Code.Ldarg) && ((int) instr.Operand == 0))
					return RuleResult.Success;
			}

			Runner.Report (method, Severity.Low, Confidence.Total);
			return RuleResult.Failure;
		}
	}
}
