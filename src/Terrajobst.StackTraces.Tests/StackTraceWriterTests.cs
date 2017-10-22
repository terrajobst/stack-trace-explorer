using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Terrajobst.StackTraces.Tests.Helpers;
using Xunit;

namespace Terrajobst.StackTraces.Tests
{
    public class StackTraceWriterTests
    {
        private static void AssertIsMatch(string sourceWithMarkers, string expectedStackTrace)
        {
            var annotatedSource = AnnotatedText.Parse(sourceWithMarkers);

            var syntaxTree = CSharpSyntaxTree.ParseText(annotatedSource.Text);
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true);
            var compilation = CSharpCompilation.Create("dummy", new[] { syntaxTree }, new[] { mscorlib }, options);

            var runtimeStackTrace = RunAndGetStackTrace(compilation);

            WriteStackTraceAndMethods(compilation, runtimeStackTrace, out var producedStackTrace, out var methods);

            Assert.Equal(AnnotatedText.NormalizeCode(expectedStackTrace), producedStackTrace);

            var root = syntaxTree.GetRoot();
            var markedNodes = annotatedSource.Spans.Select(s => root.FindNode(s)).Reverse().ToArray();
            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            Assert.Equal(markedNodes.Length, methods.Length);

            for (var i = 0; i < markedNodes.Length; i++)
            {
                var expectedSymbol = semanticModel.GetDeclaredSymbol(markedNodes[i]);
                Assert.NotNull(expectedSymbol);

                var actualSymbol = methods[i];
                Assert.Equal(expectedSymbol, methods[i]);
            }
        }

        private static string RunAndGetStackTrace(CSharpCompilation compilation)
        {
            var actualStackTrace = (string)null;

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var sb = new StringBuilder();
                    foreach (var d in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                        sb.AppendLine(d.ToString());

                    throw new Exception("Failing to compile:" + sb);
                }

                Assert.True(result.Success);
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                try
                {
                    assembly.EntryPoint.Invoke(null, null);
                }
                catch (TargetInvocationException ex)
                {
                    actualStackTrace = ex.InnerException.ToString();
                }

                Assert.NotNull(actualStackTrace);
            }

            return actualStackTrace;
        }

        private static void WriteStackTraceAndMethods(CSharpCompilation compilation, string actualStackTrace, out string stackTrace, out ImmutableArray<IMethodSymbol> methods)
        {
            var recorder = new RecordingCompilationStackTraceWriter(compilation);
            StackTraceWriter.Write(actualStackTrace, recorder);

            stackTrace = recorder.GetText();
            methods = recorder.GetMethods();
        }

        [Fact]
        public void StackTraceWriter_Constructor()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        new Customer();
                    }
                }

                public class Customer
                {
                    public {{Customer}}()
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Customer.Customer()
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test();
                    }

                    public static void {{Test}}()
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test()
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_GenericMethod()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test<float>(1.0f);
                    }

                    public static void {{Test}}<T>(T x)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test<T>(T)
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_GenericType()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        var g = new GenericType<int>();
                        g.Test(2);
                    }
                }

                class GenericType<T>
                {
                    public void {{Test}}(T value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at GenericType<T>.Test(T)
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Overloaded()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test(2);
                    }

                    static void {{Test}}(int value)
                    {
                        throw new Exception(""Boom!"");
                    }

                    static void Test(float value)
                    {
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(int)
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Argument_Out()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test(out var x);
                    }

                    static void {{Test}}(out int value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(out int)
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Argument_Ref()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        var x = 0;
                        Test(ref x);
                    }

                    static void {{Test}}(ref int value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(ref int)
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Argument_Array()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test(null);
                    }

                    static void {{Test}}(int[] value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(int[])
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Argument_Params()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test();
                    }

                    static void {{Test}}(params int[] value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(params int[])
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Argument_GenericInstance()
        {
            var source = @"
                using System;
                using System.Collections.Generic;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test(null);
                    }

                    static void {{Test}}(IEnumerable<int> value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(IEnumerable<int>)
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Argument_GenericInstance_Array()
        {
            var source = @"
                using System;
                using System.Collections.Generic;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Test(null);
                    }

                    static void {{Test}}(IEnumerable<int>[] value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(IEnumerable<int>[])
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Method_Argument_Pointer()
        {
            var source = @"
                using System;
                using System.Collections.Generic;

                unsafe static class Program
                {
                    public static void {{Main}}()
                    {
                        Test(null);
                    }

                    unsafe static void {{Test}}(int* value)
                    {
                        throw new Exception(""Boom!"");
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Test(int*)
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Property_Getter()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Console.WriteLine(X);
                    }

                    public static int X
                    {
                        {{get}}
                        {
                            throw new Exception(""Boom!"");
                        }
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.X.get
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Property_Setter()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        X = 10;
                    }

                    public static int X
                    {
                        get => 42;
                        {{set}}
                        {
                            throw new Exception(""Boom!"");
                        }
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.X.set
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Event_Adder()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Changed += null;
                    }

                    public static event EventHandler Changed
                    {
                        {{add}}
                        {
                            throw new Exception(""Boom!"");
                        }
                        remove { }
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Changed.add
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }

        [Fact]
        public void StackTraceWriter_Event_Remover()
        {
            var source = @"
                using System;

                public static class Program
                {
                    public static void {{Main}}()
                    {
                        Changed -= null;
                    }

                    public static event EventHandler Changed
                    {
                        add { }
                        {{remove}}
                        {
                            throw new Exception(""Boom!"");
                        }
                    }
                }
            ";

            var expected = @"
                System.Exception: Boom!
                   at Program.Changed.remove
                   at Program.Main()
            ";

            AssertIsMatch(source, expected);
        }
    }
}
