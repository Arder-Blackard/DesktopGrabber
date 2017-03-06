using System;

namespace TestDxConsole
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            using ( var grabber = new Grabber() )
            {
                grabber.Run();
            }
        }
    }
}
