using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyTask
{
    class Program
    {
        static void Main(string[] args)
        {

            var parent = Task.Factory.StartNew(() =>
            {
                Console.WriteLine("Parent Task Run");

                var child = Task.Factory.StartNew(() =>
                {
                    Console.WriteLine("Child Task Run");
                    Thread.Sleep(5000);
                    Console.WriteLine("Child Task Completing");

                },TaskCreationOptions.AttachedToParent);

            });

            parent.Wait();
            Console.WriteLine("parent has completed.");

            Console.ReadKey();
        }
    }
}
