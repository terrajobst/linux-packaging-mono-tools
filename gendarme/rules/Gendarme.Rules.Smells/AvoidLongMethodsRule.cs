//
// Gendarme.Rules.Smells.AvoidLongMethodsRule class
//
// Authors:
//	Néstor Salceda <nestor.salceda@gmail.com>
//
// 	(C) 2007-2008 Néstor Salceda
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

using System;
using System.Collections.Generic;

using Mono.Cecil;
using Mono.Cecil.Cil;

using Gendarme.Framework;
using Gendarme.Framework.Rocks;

namespace Gendarme.Rules.Smells {

	// TODO: What does "This will work for classes that extend those types" mean?
	// Why does the summary bring up SequencePoints? Can the defaults be changed?

	/// <summary>
	/// This rule allows developers to measure the method size. The short sized 
	/// methods allows you to maintain your code better, if you have long sized 
	/// methods perhaps you have the Long Method smell.
	///
	/// The rule will skip some well known methods, because they are autogenerated:
	/// <list type="bullet">
    	/// <item>
	/// <description><c>Gtk.Bin::Build ()</c></description>
	/// </item>
    	/// <item>
	/// <description><c>Gtk.Window::Build ()</c></description>
	/// </item>
   	/// <item>
	/// <description><c>Gtk.Dialog::Build ()</c></description>
	/// </item>
    	/// <item>
	/// <description><c>System.Windows.Forms::InitializeComponents ()</c></description> 
	/// </item>
	/// <item>
	/// <description><c>System.Workflow.Activities.StateMachineWorkflowActivity::InitializeComponents ()</c></description>
	/// </item>
	/// <item>
	/// <description><c>System.Workflow.Activities.SequentialWorkflowActivity::InitializeComponents ()</c></description>
	/// </item>
	/// <item>
	/// <description><c>System.Windows.Controls.UserControl::InitializeComponents ()</c></description>
	/// </item>
	/// </list>
	/// This will work for classes that extend those types.
	///
	/// If debugging symbols (e.g. Mono .mdb or MS .pdb) are available then the rule 
	/// will compute the number of logical source line of code (SLOC). This number
	/// represent the lines where 'SequencePoint' are present in the code. By 
	/// default the maximum SLOC is defined to 40 lines.
	///
	/// Otherwise the rule falls back onto an IL-SLOC approximation. It's quite 
	/// hard to determine how many SLOC exists based on the IL (e.g. LINQ). The metric
	/// being used is based on a screen (1024 x 768) full of source code where the 
	/// number of IL instructions were counted. By default the maximum number of IL 
	/// instructions is defined to be 165.
	/// </summary>
	/// <example>
	/// Bad example:
	/// <code>
	/// public void LongMethod ()
	/// {
	///	Console.WriteLine ("I'm writting a test, and I will fill a screen with some useless code");
	///	IList list = new ArrayList ();
	/// 	list.Add ("Foo");
	/// 	list.Add (4);
	/// 	list.Add (6);
	///	
	///	IEnumerator listEnumerator = list.GetEnumerator ();
	///	while (listEnumerator.MoveNext ()) {
	///		Console.WriteLine (listEnumerator.Current);
	///	}
	/// 
	/// 	try {
	///		list.Add ("Bar");
	///		list.Add ('a');
	///	}
	///	catch (NotSupportedException exception) {
	///		Console.WriteLine (exception.Message);
	///		Console.WriteLine (exception);
	///	}
	///	 
	///	foreach (object value in list) {
	///		Console.Write (value);
	///		Console.Write (Environment.NewLine);
	///	}
	/// 
	///	int x = 0;
	///	for (int i = 0; i &lt; 100; i++) {
	///		x++;
	///	}
	///	Console.WriteLine (x);
	///   
	///	string useless = "Useless String";
	///	if (useless.Equals ("Other useless")) {
	/// 		useless = String.Empty;
	///		Console.WriteLine ("Other useless string");
	///	}
	/// 
	///	useless = String.Concat (useless," 1");
	///	for (int j = 0; j &lt; useless.Length; j++) {
	///		if (useless[j] == 'u') {
	///			Console.WriteLine ("I have detected an u char");
	///		} else {
	///			Console.WriteLine ("I have detected an useless char");
	///		}
	///	}
	///            
	///	try {
	///		foreach (string environmentVariable in Environment.GetEnvironmentVariables ().Keys) {
	///			Console.WriteLine (environmentVariable);
	///		}
	///	}
	///	catch (System.Security.SecurityException exception) {
	///		Console.WriteLine (exception.Message);
	///		Console.WriteLine (exception);
	///	}
	/// 
	///	Console.WriteLine ("I will add more useless code !!");
	///	try {
	///		if (!(File.Exists ("foo.txt"))) {
	///			File.Create ("foo.txt");    
	///			File.Delete ("foo.txt");
	///		}
	///	}
	///	catch (IOException exception) {
	///		Console.WriteLine (exception.Message);
	///		Console.WriteLine (exception);
	///	}
	/// }
	/// </code>
	/// </example>
	/// <example>
	/// Good example:
	/// <code>
	/// public void ShortMethod ()
	/// {
	///	try {
	///		foreach (string environmentVariable in Environment.GetEnvironmentVariables ().Keys) {
	///			Console.WriteLine (environmentVariable);
	///		}
	///	}
	///	catch (System.Security.SecurityException exception) {
	///		Console.WriteLine (exception.Message);
	///		Console.WriteLine (exception);
	///	}
	/// }
	/// </code>
	/// </example>

	[Problem ("Long methods are usually hard to understand and maintain.  This method can cause problems because it contains more code than the maximum allowed.")]
	[Solution ("You should apply an Extract Method refactoring, but there are other solutions.")]
	public class AvoidLongMethodsRule : Rule,IMethodRule {

		const int AssignationRatio = 7;
		const int DefaultAmountOfElements = 13;
		static Dictionary<string, string> typeMethodDictionary;

		static AvoidLongMethodsRule ()
		{
			typeMethodDictionary = new Dictionary<string,string> (4);
			typeMethodDictionary.Add ("Gtk.Bin", "Build");
			typeMethodDictionary.Add ("Gtk.Window", "Build");
			typeMethodDictionary.Add ("Gtk.Dialog", "Build");
			typeMethodDictionary.Add ("System.Windows.Forms.Form", "InitializeComponent");
			typeMethodDictionary.Add ("System.Workflow.Activities.SequentialWorkflowActivity", "InitializeComponent");
			typeMethodDictionary.Add ("System.Workflow.Activities.StateMachineWorkflowActivity", "InitializeComponent");
			typeMethodDictionary.Add ("System.Windows.Controls.UserControl", "InitializeComponent");
		}

		public AvoidLongMethodsRule ()
		{
			MaxInstructions = 165;
			MaxSourceLineOfCode = 40;
		}

		public int MaxInstructions { get; set; }

		public int MaxSourceLineOfCode { get; set; }

		// Use IL to approximate the size of the method. By default logical
		// source lines are used (when debugging symbols are available).
		public bool UseIlApproximation { get; set; }


		private static bool IsAutogeneratedByTools (MethodDefinition method)
		{
			if (method.HasParameters)
				return false;

			TypeDefinition type = method.DeclaringType.Resolve ();
			if ((type != null) && (type.BaseType != null)) {
				string method_name;
				if (typeMethodDictionary.TryGetValue (type.BaseType.FullName, out method_name)) {
					return (method_name == method.Name);
				}
			}
			return false;
		}

		private static int CountStaticFields (TypeDefinition type)
		{
			int counter = 0;
			foreach (FieldDefinition field in type.Fields) {
				if (field.IsStatic || field.HasConstant)
					counter++;
				//if the field is an array, we should take care
				//about their elements.
				ArrayType array = field.FieldType as ArrayType;
				if (array != null) {
					for (int index = 0; index < array.Dimensions.Count; index++)  
						//I can't calculate the array
						//length, then, i add a
						//default amount of elements 
						//TODO: Perhaps we can do other
						//approach with random nunbers?
						counter+= DefaultAmountOfElements;
				}
			}
			return counter;
		}

		private static int CountInstanceFields (TypeDefinition type) 
		{
			int counter = 0;
			foreach (FieldDefinition field in type.Fields) {
				if (!(field.IsStatic || field.HasConstant))
					counter++;
				//I not take care about arrays here.
			}
			return counter;
		}

		private static int GetFieldCount (TypeDefinition type, bool staticFields)
		{
			if (!type.HasFields)
				return 0;
			return staticFields ? CountStaticFields (type) : CountInstanceFields (type);
		}

		private static int CountSourceLinesOfCode (MethodDefinition method)
		{
			int sloc = 0;
			int current_line = -1;
			foreach (Instruction ins in method.Body.Instructions) {
				SequencePoint sp = ins.SequencePoint;
				if (sp == null)
					continue;

				int line = sp.StartLine;
				// special value for PDB (so that debuggers can ignore a line)
				if (line == 0xFEEFEE)
					continue;

				// lines numbers may not be ordered (loops) or reused several times
				if ((current_line == -1) || (line > current_line))
					sloc++;
				current_line = line;
			}
			return sloc;
		}

		private static int CountInstructions (MethodDefinition method)
		{
			int count = 0;
			foreach (Instruction ins in method.Body.Instructions) {
				switch (ins.OpCode.Code) {
				case Code.Nop:
				case Code.Box:
				case Code.Unbox:
					break;
				default:
					count++;
					break;
				}
			}
			return count;
		}

		public RuleResult CheckMethod (MethodDefinition method)
		{
			// rule does not apply if method as no code (e.g. abstract, p/invoke)
			// rule does not apply to code outside the developer's control
			// rule does not apply to autogenerated code from some tools
			if (!method.HasBody || method.IsGeneratedCode () || IsAutogeneratedByTools (method))
				return RuleResult.DoesNotApply;

			// rule applies!

			int field_count = 0;
			if (method.IsConstructor)
				field_count = GetFieldCount ((method.DeclaringType as TypeDefinition), method.IsStatic);

			// if we have debugging information available and we're not asked to use IL approximation
			if (!UseIlApproximation && method.DeclaringType.Module.HasSymbols) {
				// add a few extra lines to let the constructors initialize the fields
				int max = MaxSourceLineOfCode + field_count;
				int sloc = CountSourceLinesOfCode (method);
				if (sloc <= max)
					return RuleResult.Success;

				string message = String.Format ("Logical SLOC: {0}. Maximum : {1}", sloc, max);
				Runner.Report (method, Severity.High, Confidence.High, message);
			} else {
				// success if the instruction count is below the defined threshold
				// add a few extra lines to let the constructors initialize the fields
				int max = MaxInstructions + field_count * AssignationRatio;
				int count = CountInstructions (method);
				if (count <= max)
					return RuleResult.Success;

				string message = String.Format ("Method IL Size: {0}. Maximum Size: {1}", count, max);
				Runner.Report (method, Severity.High, Confidence.Normal, message);
			}

			return RuleResult.Failure;
		}
	}
}