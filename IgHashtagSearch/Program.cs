using System;
using System.Threading;

namespace IgHashtagSearch
{
    class Program
    {
        static int _downloads = 0;
        static int Downloads
        {
            get { return _downloads; }
            set { _downloads = value; Console.WriteLine(value.ToString()); Console.Title = value.ToString(); }
        }

        static LocationGrabber grabber = null;
        static string lastCursor = "1793750843284319814";
        
        static void Main(string[] args)
        {
            //run();
            //Console.ReadLine();
            //return;

            grabber = new LocationGrabber("504830974", "governors-island", new DateTime(2017, 6, 2));
            grabber.OnDownloadCountChanged += (s, e) => Console.Title = "Current downloading files: " + grabber.CurrentDownloadCount;
            grabber.OnNewIdentityRequired += (s, e) =>
            {
                Console.WriteLine("New identity required.");
                Thread.Sleep(TimeSpan.FromMinutes(4));
                lastCursor = grabber.EndCursor;
                startGrabber();
            };

            startGrabber();

            Console.WriteLine("Done");
            Console.ReadLine();
        }
        
        static void startGrabber()
        {
            grabber.Init(lastCursor);
            grabber.Start();
        }
    }
}
