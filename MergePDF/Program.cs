using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using iTextSharp.text.log;
using Nancy.Hosting.Self;

namespace MergePDF
{
    class Program
    {
        static void Main(string[] args)
        {
            CounterFactory.getInstance().SetCounter(new AGPLWarningRemoverCounter());

            var uri = "http://localhost:8888";
            Console.WriteLine("Starting Nancy on " + uri);

            // initialize an instance of NancyHost
            var host = new NancyHost(new HostConfiguration()
            {
                UrlReservations = new UrlReservations()
                {
                    CreateAutomatically = true
                },
                RewriteLocalhost = true
            }, new Uri(uri));


            host.Start();  // start hosting

            Console.ReadLine();

            Console.WriteLine("Stopping Nancy");
            host.Stop();  // stop hosting
        }


    }

    public class AGPLWarningRemoverCounter : ICounter
    {
        public AGPLWarningRemoverCounter()
        {
        }

        public ICounter GetCounter(Type klass)
        {
            return this;
        }

        public void Read(long l)
        {
            
        }

        public void Written(long l)
        {

        }
    }
}
