namespace TestConsole
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var c = new Asdf();
            var a = new Asdf() { };
        }
    }

    public class Asdf
    {
        public int A { get; set; }
        public required int B {  get; init; }
    }
}