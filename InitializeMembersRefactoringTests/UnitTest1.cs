using InitializeMembersRefactoring;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeRefactoringVerifier<
    InitializeMembersRefactoring.InitializeMembersRefactoringCodeRefactoringProvider,
    Microsoft.CodeAnalysis.Testing.Verifiers.XUnitVerifier>;

namespace InitializeMembersRefactoringTests
{
    public class UnitTest1
    {
        [Fact(Skip = "Fix location")]
        public async Task Test1()
        {
            string src = @"
namespace TestConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var a = new Asdf() { };
        }
    }

    public class Asdf
    {
        public int A { get; set; }
    }
}
";
            string fix = @"
namespace TestConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var a = new Asdf() { A = };
        }
    }

    public class Asdf
    {
        public int A { get; set; }
    }
}
";
            var loc = new DiagnosticResult().WithLocation(8, 38);
            await VerifyCS.VerifyRefactoringAsync(src, loc, fix);
        }
    }
}